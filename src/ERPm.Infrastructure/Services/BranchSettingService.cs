using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ERPm.Application.Common;
using ERPm.Application.Common.Interfaces;
using ERPm.Application.DTOs.System;
using ERPm.Application.Features.System;
using ERPm.Domain.Entities.System;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace ERPm.Infrastructure.Services;

/// <summary>
/// Branch settings resolver and writer. A key resolves to the branch's own override, else the nearest active
/// ancestor's override inside the same company, else the definition default.
/// </summary>
public sealed partial class BranchSettingService(ApplicationDbContext db, ICurrentUser user, IEnumerable<IBranchSettingModuleProvider> modules) : IBranchSettingService
{
    public const string ViewAction = "sys:branch-setting:view";
    public const string EditAction = "sys:branch-setting:edit";
    public const string InvalidValue = "error.branch_setting.invalid_value";
    public const string NotFound = "error.branch_setting.not_found";
    public const string DuplicateKey = "error.branch_setting.duplicate_key";
    public const string ModuleManaged = "error.branch_setting.module_managed";
    public const string StaleRevision = "error.branch_setting.stale_revision";
    public const string RevisionRequired = "error.branch_setting.revision_required";
    public const string AccessDenied = "error.access.denied";
    private const long MaxSafeInteger = 9007199254740991;
    private static readonly string[] ValueTypes = ["boolean", "integer", "decimal", "time", "string", "choice"];

    [GeneratedRegex("^[a-z][a-z0-9_]*(\\.[a-z][a-z0-9_]*)+$")] private static partial Regex KeyPattern();
    [GeneratedRegex("^[a-z][a-z0-9_]*$")] private static partial Regex SegmentPattern();
    [GeneratedRegex("^-?(0|[1-9][0-9]{0,13})(\\.[0-9]{1,4})?$")] private static partial Regex DecimalPattern();
    [GeneratedRegex("^([01][0-9]|2[0-3]):[0-5][0-9]$")] private static partial Regex TimePattern();

    private sealed record Node(int Id, string Name, int? ParentId, bool IsActive, OrgUnitType Type);
    private sealed record Resolved(string? ValueJson, string SourceKind, int? SourceId, bool Valid);

    /// <summary>One company's tree with the live overrides of the requested keys.</summary>
    private sealed class Tree(Dictionary<int, Node> nodes, Dictionary<(int, string), BranchSetting> overrides)
    {
        public Dictionary<int, Node> Nodes { get; } = nodes;
        public Dictionary<(int Branch, string Key), BranchSetting> Overrides { get; } = overrides;

        public IEnumerable<Node> Chain(int id)
        {
            // The hop limit stops a corrupted parent cycle from looping forever.
            var hops = 0;
            for (var node = Nodes.GetValueOrDefault(id); node != null && hops++ <= Nodes.Count; node = node.ParentId is { } p ? Nodes.GetValueOrDefault(p) : null)
                yield return node;
        }

        public List<Node> Descendants(int id)
        {
            var result = new List<Node>();
            var queue = new Queue<int>([id]);
            while (queue.Count > 0 && result.Count <= Nodes.Count)
            {
                var parent = queue.Dequeue();
                foreach (var child in Nodes.Values.Where(n => n.ParentId == parent && n.Id != id))
                { result.Add(child); queue.Enqueue(child.Id); }
            }
            return result;
        }
    }

    // ---------- Definitions ----------

    public async Task<List<SettingDefinitionDto>> GetDefinitionsAsync(string? group, CancellationToken ct = default)
    {
        var rows = await db.SettingDefinitions.AsNoTracking().Where(d => group == null || d.GroupKey == group)
            .OrderBy(d => d.GroupKey).ThenBy(d => d.SortOrder).ThenBy(d => d.Key).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<SettingDefinitionDto> CreateDefinitionAsync(CreateSettingDefinitionRequest request, CancellationToken ct = default)
    {
        var key = request.Key?.Trim() ?? string.Empty;
        var type = request.ValueType ?? string.Empty;
        string? vi = null, en = null;
        PostingTransaction.Require(key.Length <= 96 && KeyPattern().IsMatch(key)
            && request.GroupKey is { Length: > 0 and <= 64 } groupKey && SegmentPattern().IsMatch(groupKey)
            && ValueTypes.Contains(type)
            && request.Labels != null && request.Labels.TryGetValue("vi", out vi) && request.Labels.TryGetValue("en", out en)
            && IsLabel(vi) && IsLabel(en), InvalidValue);
        var constraints = NormalizeConstraints(type, request.Constraints);
        PostingTransaction.Require(constraints != null && IsValidValue(type, Parse(constraints), request.DefaultValue), InvalidValue);
        PostingTransaction.Require(!await db.SettingDefinitions.IgnoreQueryFilters().AnyAsync(d => d.Key == key, ct), DuplicateKey, 409);

        var definition = new SettingDefinition
        {
            Key = key,
            GroupKey = request.GroupKey!,
            ValueType = type,
            LabelsJson = JsonSerializer.Serialize(new Dictionary<string, string> { ["vi"] = vi!.Trim(), ["en"] = en!.Trim() }),
            ConstraintsJson = constraints!,
            DefaultValueJson = request.DefaultValue.GetRawText(),
            StorageKind = SettingDefinition.Scalar,
            SortOrder = request.SortOrder
        };
        db.SettingDefinitions.Add(definition);
        await db.SaveChangesAsync(ct);
        return ToDto(definition);
    }

    // ---------- Reads ----------

    public async Task<SettingListDto> GetSettingsAsync(int branchId, string? group, CancellationToken ct = default)
    {
        var target = await TargetAsync(branchId, ct);
        var definitions = await db.SettingDefinitions.AsNoTracking().Where(d => group == null || d.GroupKey == group)
            .OrderBy(d => d.GroupKey).ThenBy(d => d.SortOrder).ThenBy(d => d.Key).ToListAsync(ct);
        var scalarKeys = definitions.Where(d => d.StorageKind == SettingDefinition.Scalar).Select(d => d.Key).ToArray();
        var tree = await LoadTreeAsync(target, scalarKeys, track: false, ct);
        var items = new List<SettingViewDto>();
        foreach (var definition in definitions)
        {
            if (definition.StorageKind == SettingDefinition.Scalar) items.Add(View(definition, target.Id, tree));
            else if (Module(definition.Key) is { } provider) items.Add(await provider.GetAsync(branchId, ct));
        }
        return new SettingListDto { Items = items };
    }

    public async Task<SettingViewDto> GetSettingAsync(int branchId, string key, CancellationToken ct = default)
    {
        var target = await TargetAsync(branchId, ct);
        var definition = await DefinitionAsync(key, ct);
        if (definition.StorageKind != SettingDefinition.Scalar)
            return await (Module(key) ?? throw new BusinessException(NotFound, 404)).GetAsync(branchId, ct);
        return View(definition, target.Id, await LoadTreeAsync(target, [key], track: false, ct));
    }

    public async Task<PagedResult<SettingHistoryItemDto>> GetHistoryAsync(int branchId, string key, int pageIndex, int pageSize, CancellationToken ct = default)
    {
        await TargetAsync(branchId, ct);
        await DefinitionAsync(key, ct);
        pageIndex = Math.Max(pageIndex, 1);
        pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);
        var query = db.BranchSettingHistories.AsNoTracking().Where(h => h.BranchId == branchId && h.SettingKey == key);
        var rows = await query.OrderByDescending(h => h.Id).Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<SettingHistoryItemDto>
        {
            TotalCount = await query.CountAsync(ct),
            PageIndex = pageIndex,
            PageSize = pageSize,
            Items = rows.Select(h => new SettingHistoryItemDto
            {
                BranchId = h.BranchId, Key = h.SettingKey, PreviousLocal = Local(h.BeforeJson), NewLocal = Local(h.AfterJson),
                ChangedBy = h.ChangedBy, ChangedAtUtc = h.ChangedAtUtc, Reason = h.Reason, Revision = h.Revision
            }).ToList()
        };
    }

    // ---------- Writes ----------

    public async Task<SettingImpactDto> PreviewAsync(int branchId, string key, UpdateBranchSettingRequest request, string? ifMatch, CancellationToken ct = default)
        => (await PlanAsync(branchId, key, request, ifMatch, track: false, ct)).Impact;

    public async Task<SettingViewDto> UpdateAsync(int branchId, string key, UpdateBranchSettingRequest request, string? ifMatch, CancellationToken ct = default)
    {
        var posting = new PostingTransaction(db);
        return await posting.RunAsync(async () =>
        {
            var company = await db.Branches.IgnoreQueryFilters().Where(b => b.Id == branchId).Select(b => b.CompanyId).FirstOrDefaultAsync(ct);
            // One lock per company and key: the impact of a write depends on every override of the key in the tree.
            await posting.LockAsync($"branch-setting:{company?.ToString(CultureInfo.InvariantCulture) ?? "b" + branchId}:{key}", ct);
            var plan = await PlanAsync(branchId, key, request, ifMatch, track: true, ct);
            var current = plan.Tree.Overrides.GetValueOrDefault((branchId, key));
            var before = current?.ValueJson;
            var after = plan.NewValueJson;
            // Saving the same local state again is a no-op without a history row.
            if (SameJson(before, after)) return View(plan.Definition, branchId, plan.Tree);

            var revision = current?.Revision + 1 ?? (await db.BranchSettingHistories.Where(h => h.BranchId == branchId && h.SettingKey == key)
                .MaxAsync(h => (int?)h.Revision, ct) ?? 0) + 1;
            if (after == null)
            {
                current!.IsDeleted = true;
                current.DeletedAtUtc = DateTime.UtcNow;
                current.DeletedBy = user.UserId;
                current.Revision = revision;
                plan.Tree.Overrides.Remove((branchId, key));
            }
            else if (current != null) { current.ValueJson = after; current.Revision = revision; }
            else
            {
                var row = new BranchSetting { BranchId = branchId, SettingKey = key, ValueJson = after, Revision = revision };
                db.BranchSettings.Add(row);
                plan.Tree.Overrides[(branchId, key)] = row;
            }
            db.BranchSettingHistories.Add(new BranchSettingHistory
            {
                BranchId = branchId, SettingKey = key, BeforeJson = before, AfterJson = after,
                ChangedBy = user.UserId, Reason = plan.Reason, Revision = revision
            });
            await db.SaveChangesAsync(ct);
            return View(plan.Definition, branchId, plan.Tree);
        }, ct);
    }

    private sealed record Plan(SettingDefinition Definition, Tree Tree, string? NewValueJson, string? Reason, SettingImpactDto Impact);

    private async Task<Plan> PlanAsync(int branchId, string key, UpdateBranchSettingRequest request, string? ifMatch, bool track, CancellationToken ct)
    {
        var reason = request.Reason?.Trim();
        PostingTransaction.Require(reason is not { Length: > 500 }
            && (request.Mode == "inherit" && request.Value.ValueKind == JsonValueKind.Undefined
                || request.Mode == "override" && request.Value.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null)), InvalidValue);
        var target = await TargetAsync(branchId, ct);
        var definition = await DefinitionAsync(key, ct);
        PostingTransaction.Require(definition.StorageKind == SettingDefinition.Scalar, ModuleManaged, 409);
        PostingTransaction.Require(user.CanAt(EditAction, branchId), AccessDenied, 403);
        PostingTransaction.Require(!string.IsNullOrWhiteSpace(ifMatch), RevisionRequired, 428);
        var tree = await LoadTreeAsync(target, [key], track, ct);
        PostingTransaction.Require(ifMatch!.Trim() == Etag(target.Id, key, tree), StaleRevision, 412);
        string? newValue = null;
        if (request.Mode == "override")
        {
            PostingTransaction.Require(IsValidValue(definition.ValueType, Parse(definition.ConstraintsJson), request.Value), InvalidValue);
            newValue = request.Value.GetRawText();
        }

        // Effective values of the active descendants before and after the change; the target itself is in the returned view.
        var descendants = tree.Descendants(target.Id).Where(IsSettable).ToList();
        var before = descendants.ToDictionary(n => n.Id, n => Resolve(definition, n.Id, tree));
        var simulated = new Tree(tree.Nodes, new Dictionary<(int, string), BranchSetting>(tree.Overrides));
        if (newValue == null) simulated.Overrides.Remove((branchId, key));
        else simulated.Overrides[(branchId, key)] = new BranchSetting { BranchId = branchId, SettingKey = key, ValueJson = newValue };
        var changes = new List<SettingImpactChangeDto>();
        foreach (var node in descendants)
        {
            var (was, now) = (before[node.Id], Resolve(definition, node.Id, simulated));
            if (was.SourceKind == now.SourceKind && was.SourceId == now.SourceId && SameJson(was.ValueJson, now.ValueJson)) continue;
            // A change reaching a unit outside the caller's edit scope is refused without listing it.
            PostingTransaction.Require(user.CanAt(EditAction, node.Id), AccessDenied, 403);
            changes.Add(new SettingImpactChangeDto
            {
                BranchId = node.Id, BranchName = node.Name,
                Before = new SettingImpactSideDto { Value = Element(was.ValueJson), SourceKind = was.SourceKind },
                After = new SettingImpactSideDto { Value = Element(now.ValueJson), SourceKind = now.SourceKind }
            });
        }
        return new Plan(definition, tree, newValue, string.IsNullOrEmpty(reason) ? null : reason, new SettingImpactDto { AffectedBranchCount = changes.Count, Changes = changes });
    }

    // ---------- Resolver ----------

    private static bool IsSettable(Node node) => node.IsActive && node.Type is OrgUnitType.Company or OrgUnitType.Branch;

    private static Resolved Resolve(SettingDefinition definition, int branchId, Tree tree)
    {
        foreach (var node in tree.Chain(branchId))
        {
            // Inactive units are skipped; their children keep inheriting from the next active ancestor.
            if (!node.IsActive || !tree.Overrides.TryGetValue((node.Id, definition.Key), out var row)) continue;
            var valid = IsValidValue(definition.ValueType, Parse(definition.ConstraintsJson), Parse(row.ValueJson));
            return new Resolved(valid ? row.ValueJson : null, node.Id == branchId ? "local" : "ancestor", node.Id, valid);
        }
        return new Resolved(definition.DefaultValueJson, "system_default", null, definition.DefaultValueJson != null);
    }

    private SettingViewDto View(SettingDefinition definition, int branchId, Tree tree)
    {
        var resolved = Resolve(definition, branchId, tree);
        var local = tree.Overrides.GetValueOrDefault((branchId, definition.Key));
        var source = new SettingSourceDto { Kind = resolved.SourceKind };
        if (resolved.SourceId is { } sourceId && user.CanAt(ViewAction, sourceId))
        {
            source.BranchId = sourceId;
            source.BranchName = tree.Nodes[sourceId].Name;
        }
        return new SettingViewDto
        {
            BranchId = branchId,
            Key = definition.Key,
            StorageKind = definition.StorageKind,
            ValueType = definition.ValueType,
            EditorKey = definition.EditorKey,
            Local = local == null ? new SettingLocalDto { Mode = "inherit" } : new SettingLocalDto { Mode = "override", Value = Element(local.ValueJson) },
            Effective = resolved.Valid
                ? new SettingEffectiveDto { Status = "resolved", Value = Element(resolved.ValueJson), Source = source }
                : new SettingEffectiveDto { Status = "invalid", ErrorCode = InvalidValue, Source = source },
            CanEdit = user.CanAt(EditAction, branchId),
            Etag = Etag(branchId, definition.Key, tree)
        };
    }

    /// <summary>
    /// Entity tag over everything the view and the impact of a write depend on: ancestors, the unit and its subtree
    /// (parent links, active flags) and the revisions of their overrides of the key.
    /// </summary>
    private static string Etag(int branchId, string key, Tree tree)
    {
        var relevant = tree.Chain(branchId).Concat(tree.Descendants(branchId)).OrderBy(n => n.Id)
            .Select(n => $"{n.Id}:{n.ParentId}:{n.IsActive}:{tree.Overrides.GetValueOrDefault((n.Id, key))?.Revision}");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key + "|" + string.Join(";", relevant)));
        return $"\"bs-{branchId}-{Convert.ToHexStringLower(hash, 0, 8)}\"";
    }

    private async Task<Node> TargetAsync(int branchId, CancellationToken ct)
    {
        var node = await db.Branches.IgnoreQueryFilters().AsNoTracking().Where(b => b.Id == branchId && !b.IsDeleted)
            .Select(b => new Node(b.Id, b.Name, b.ParentId, b.IsActive, b.OrgUnitType)).FirstOrDefaultAsync(ct);
        PostingTransaction.Require(node != null && IsSettable(node) && user.CanAt(ViewAction, branchId), NotFound, 404);
        return node!;
    }

    private async Task<SettingDefinition> DefinitionAsync(string key, CancellationToken ct) =>
        await db.SettingDefinitions.AsNoTracking().FirstOrDefaultAsync(d => d.Key == key, ct) ?? throw new BusinessException(NotFound, 404);

    /// <summary>Loads the units of the target's company (only the target when it has none) and their live overrides.</summary>
    private async Task<Tree> LoadTreeAsync(Node target, string[] keys, bool track, CancellationToken ct)
    {
        var company = await db.Branches.IgnoreQueryFilters().Where(b => b.Id == target.Id).Select(b => b.CompanyId).FirstAsync(ct);
        var nodes = await db.Branches.IgnoreQueryFilters().AsNoTracking()
            .Where(b => !b.IsDeleted && (company == null ? b.Id == target.Id : b.CompanyId == company))
            .Select(b => new Node(b.Id, b.Name, b.ParentId, b.IsActive, b.OrgUnitType)).ToDictionaryAsync(n => n.Id, ct);
        var ids = nodes.Keys.ToArray();
        var query = db.BranchSettings.Where(s => ids.Contains(s.BranchId) && keys.Contains(s.SettingKey));
        var rows = await (track ? query : query.AsNoTracking()).ToListAsync(ct);
        return new Tree(nodes, rows.ToDictionary(r => (r.BranchId, r.SettingKey)));
    }

    private IBranchSettingModuleProvider? Module(string key) => modules.FirstOrDefault(m => m.Key == key);

    // ---------- Type validation ----------

    /// <summary>Validates and normalizes the constraints of a value type; null when they do not fit the type.</summary>
    public static string? NormalizeConstraints(string type, JsonElement constraints)
    {
        if (constraints.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return "{}";
        if (constraints.ValueKind != JsonValueKind.Object) return null;
        var allowed = type switch
        {
            "integer" or "decimal" => new[] { "min", "max" },
            "string" => ["maxLength", "allowEmpty"],
            "choice" => ["options"],
            _ => []
        };
        foreach (var property in constraints.EnumerateObject())
            if (!allowed.Contains(property.Name)) return null;
        bool Has(string name, out JsonElement value) => constraints.TryGetProperty(name, out value);
        switch (type)
        {
            case "integer":
                long? min = Has("min", out var a) ? (IsValidValue("integer", default, a) ? a.GetInt64() : null) : long.MinValue;
                long? max = Has("max", out var b) ? (IsValidValue("integer", default, b) ? b.GetInt64() : null) : long.MaxValue;
                if (min == null || max == null || min > max) return null;
                break;
            case "decimal":
                decimal? dmin = Has("min", out var c) ? (IsValidValue("decimal", default, c) ? decimal.Parse(c.GetString()!, CultureInfo.InvariantCulture) : null) : decimal.MinValue;
                decimal? dmax = Has("max", out var d) ? (IsValidValue("decimal", default, d) ? decimal.Parse(d.GetString()!, CultureInfo.InvariantCulture) : null) : decimal.MaxValue;
                if (dmin == null || dmax == null || dmin > dmax) return null;
                break;
            case "string":
                if (Has("maxLength", out var length) && !(length.ValueKind == JsonValueKind.Number && length.TryGetInt32(out var n) && n is >= 1 and <= 4000)) return null;
                if (Has("allowEmpty", out var empty) && empty.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return null;
                break;
            case "choice":
                if (!Has("options", out var options) || options.ValueKind != JsonValueKind.Array || options.GetArrayLength() is < 1 or > 100) return null;
                var seen = new HashSet<string>();
                foreach (var option in options.EnumerateArray())
                {
                    if (option.ValueKind != JsonValueKind.Object || option.EnumerateObject().Any(p => p.Name is not ("value" or "labels"))
                        || !option.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.String
                        || value.GetString() is not { Length: > 0 and <= 64 } code || !seen.Add(code)
                        || !option.TryGetProperty("labels", out var labels) || labels.ValueKind != JsonValueKind.Object
                        || !labels.TryGetProperty("vi", out var vi) || !labels.TryGetProperty("en", out var en)
                        || vi.ValueKind != JsonValueKind.String || en.ValueKind != JsonValueKind.String || !IsLabel(vi.GetString()) || !IsLabel(en.GetString()))
                        return null;
                }
                break;
        }
        return constraints.GetRawText();
    }

    /// <summary>Checks a value against its type and (already validated) constraints. JSON null is never a value.</summary>
    public static bool IsValidValue(string type, JsonElement constraints, JsonElement value)
    {
        bool Has(string name, out JsonElement v) { v = default; return constraints.ValueKind == JsonValueKind.Object && constraints.TryGetProperty(name, out v); }
        switch (type)
        {
            case "boolean":
                return value.ValueKind is JsonValueKind.True or JsonValueKind.False;
            case "integer":
                return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var n) && Math.Abs(n) <= MaxSafeInteger
                    && (!Has("min", out var min) || n >= min.GetInt64()) && (!Has("max", out var max) || n <= max.GetInt64());
            case "decimal":
                if (value.ValueKind != JsonValueKind.String || value.GetString() is not { } text || !DecimalPattern().IsMatch(text)) return false;
                var d = decimal.Parse(text, CultureInfo.InvariantCulture);
                return (!Has("min", out var dmin) || d >= decimal.Parse(dmin.GetString()!, CultureInfo.InvariantCulture))
                    && (!Has("max", out var dmax) || d <= decimal.Parse(dmax.GetString()!, CultureInfo.InvariantCulture));
            case "time":
                return value.ValueKind == JsonValueKind.String && TimePattern().IsMatch(value.GetString()!);
            case "string":
                if (value.ValueKind != JsonValueKind.String) return false;
                var s = value.GetString()!;
                var limit = Has("maxLength", out var length) ? length.GetInt32() : 4000;
                return s.Length <= limit && (s.Length > 0 || Has("allowEmpty", out var empty) && empty.ValueKind == JsonValueKind.True);
            case "choice":
                return value.ValueKind == JsonValueKind.String && Has("options", out var options)
                    && options.EnumerateArray().Any(o => o.GetProperty("value").GetString() == value.GetString());
            default:
                return false;
        }
    }

    private static bool IsLabel(string? label) => label is { } l && l.Trim().Length is > 0 and <= 200;
    private static JsonElement Parse(string json) => JsonSerializer.Deserialize<JsonElement>(json);
    private static JsonElement? Element(string? json) => json == null ? null : Parse(json);
    private static bool SameJson(string? a, string? b) => a == null || b == null ? a == b : JsonElement.DeepEquals(Parse(a), Parse(b));
    private static SettingLocalDto Local(string? json) => json == null ? new SettingLocalDto { Mode = "inherit" } : new SettingLocalDto { Mode = "override", Value = Parse(json) };

    private static SettingDefinitionDto ToDto(SettingDefinition d) => new()
    {
        Key = d.Key,
        GroupKey = d.GroupKey,
        ValueType = d.ValueType,
        Labels = JsonSerializer.Deserialize<Dictionary<string, string>>(d.LabelsJson) ?? new(),
        Constraints = Parse(d.ConstraintsJson),
        DefaultValue = Element(d.DefaultValueJson),
        StorageKind = d.StorageKind,
        EditorKey = d.EditorKey,
        SortOrder = d.SortOrder
    };
}
