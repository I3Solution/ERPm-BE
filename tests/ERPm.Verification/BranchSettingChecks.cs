using System.Text.Json;
using ERPm.Application.Common.Interfaces;
using ERPm.Application.DTOs.System;
using ERPm.Domain.Entities.System;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace ERPm.Verification;

/// <summary>Step 27 branch settings: type validation, inheritance inside a company, version guard, scope, history, preview.</summary>
public static class BranchSettingChecks
{
    public sealed record Context(
        Func<ICurrentUser?, ApplicationDbContext> Db,
        Action<bool, string> Check,
        Func<Func<Task>, string, Task> Reject);

    private static JsonElement J(string json) => JsonSerializer.Deserialize<JsonElement>(json);
    private static readonly JsonElement None = default;

    public static void Pure(Action<bool, string> check)
    {
        bool Ok(string type, string value, string constraints = "{}") => BranchSettingService.IsValidValue(type, J(constraints), J(value));
        check(Ok("boolean", "false") && Ok("integer", "0") && Ok("decimal", "\"10000.2500\"") && Ok("time", "\"07:05\"")
            && Ok("string", "\"\"", "{\"allowEmpty\":true}") && Ok("choice", "\"a\"", "{\"options\":[{\"value\":\"a\",\"labels\":{\"vi\":\"A\",\"en\":\"A\"}}]}"),
            "branch setting validator keeps false, 0, decimal strings, HH:mm, allowed empty string and listed choice");
        check(!Ok("boolean", "null") && !Ok("integer", "null") && !Ok("integer", "\"1\"") && !Ok("integer", "1.5") && !Ok("integer", "9007199254740992")
            && !Ok("integer", "11", "{\"max\":10}") && !Ok("decimal", "10") && !Ok("decimal", "\"1e3\"") && !Ok("decimal", "\"123456789012345\"")
            && !Ok("decimal", "\"1.23456\"") && !Ok("time", "\"24:00\"") && !Ok("time", "\"7:05\"") && !Ok("string", "\"\"")
            && !Ok("string", "\"abcd\"", "{\"maxLength\":3}") && !Ok("choice", "\"b\"", "{\"options\":[{\"value\":\"a\",\"labels\":{\"vi\":\"A\",\"en\":\"A\"}}]}"),
            "branch setting validator rejects null, wrong JSON kinds, unsafe integers, exponent or over-scale decimals, bad times and out-of-range values");
        check(BranchSettingService.NormalizeConstraints("boolean", J("{\"maxLength\":3}")) == null
            && BranchSettingService.NormalizeConstraints("integer", J("{\"min\":5,\"max\":1}")) == null
            && BranchSettingService.NormalizeConstraints("choice", J("{\"options\":[]}")) == null
            && BranchSettingService.NormalizeConstraints("decimal", J("{\"min\":\"0.0000\",\"max\":\"99.5\"}")) != null
            && BranchSettingService.NormalizeConstraints("time", None) == "{}",
            "branch setting constraints must fit the value type");
    }

    public static async Task RunDbChecksAsync(Context ctx)
    {
        var admin = new ScopedUser(_ => true, _ => true);
        await using var db = ctx.Db(admin);
        var service = new BranchSettingService(db, admin, []);
        var root = await db.Branches.FirstAsync(b => b.Code == "root");

        // Company A -> Hanoi -> point of sale, and an independent Company B -> branch.
        var companyA = new Branch { Code = "BS_CO_A", Name = "BS Company A", ParentId = root.Id, OrgUnitType = OrgUnitType.Company };
        var companyB = new Branch { Code = "BS_CO_B", Name = "BS Company B", ParentId = root.Id, OrgUnitType = OrgUnitType.Company };
        db.Branches.AddRange(companyA, companyB);
        await db.SaveChangesAsync();
        companyA.CompanyId = companyA.Id; companyB.CompanyId = companyB.Id;
        var hanoi = new Branch { Code = "BS_HN", Name = "BS Hanoi", ParentId = companyA.Id, CompanyId = companyA.Id };
        var b1 = new Branch { Code = "BS_B1", Name = "BS B1", ParentId = companyB.Id, CompanyId = companyB.Id };
        db.Branches.AddRange(hanoi, b1);
        await db.SaveChangesAsync();
        var pos = new Branch { Code = "BS_POS", Name = "BS POS", ParentId = hanoi.Id, CompanyId = companyA.Id };
        db.Branches.Add(pos);
        await db.SaveChangesAsync();

        CreateSettingDefinitionRequest Def(string key, string type, string defaultValue, string constraints = "{}") => new()
        {
            Key = key, GroupKey = "operations", ValueType = type, Labels = new() { ["vi"] = key, ["en"] = key },
            DefaultValue = J(defaultValue), Constraints = J(constraints), SortOrder = 10
        };
        var cutoff = await service.CreateDefinitionAsync(Def("delivery.cutoff_time", "time", "\"16:00\""));
        await service.CreateDefinitionAsync(Def("delivery.require_note", "boolean", "true"));
        await service.CreateDefinitionAsync(Def("delivery.max_lines", "integer", "5", "{\"min\":0,\"max\":100}"));
        ctx.Check(cutoff.StorageKind == "scalar" && cutoff.DefaultValue?.GetString() == "16:00", "API-created definition is scalar with a validated default");
        await ctx.Reject(() => service.CreateDefinitionAsync(Def("delivery.cutoff_time", "time", "\"10:00\"")), BranchSettingService.DuplicateKey);
        await ctx.Reject(() => service.CreateDefinitionAsync(Def("Delivery", "time", "\"10:00\"")), BranchSettingService.InvalidValue);
        await ctx.Reject(() => service.CreateDefinitionAsync(Def("delivery.no_default", "integer", "null")), BranchSettingService.InvalidValue);
        await ctx.Reject(() => service.CreateDefinitionAsync(Def("delivery.bad_limit", "integer", "500", "{\"max\":100}")), BranchSettingService.InvalidValue);

        async Task<SettingViewDto> Set(int branchId, string key, string? value, string? reason = null)
        {
            var etag = (await service.GetSettingAsync(branchId, key)).Etag;
            return await service.UpdateAsync(branchId, key, new UpdateBranchSettingRequest { Mode = value == null ? "inherit" : "override", Value = value == null ? None : J(value), Reason = reason }, etag);
        }

        var initial = await service.GetSettingAsync(pos.Id, "delivery.cutoff_time");
        ctx.Check(initial.Local.Mode == "inherit" && initial.Effective.Source?.Kind == "system_default" && initial.Effective.Source.BranchId == null
            && initial.Effective.Value?.GetString() == "16:00", "unconfigured key resolves to the definition default without a source unit");

        await Set(companyA.Id, "delivery.cutoff_time", "\"16:30\"");
        await Set(companyA.Id, "delivery.require_note", "true");
        var hanoiSet = await Set(hanoi.Id, "delivery.cutoff_time", "\"17:00\"", "  Hanoi closes later  ");
        await Set(companyB.Id, "delivery.require_note", "false");
        await Set(hanoi.Id, "delivery.max_lines", "0");

        var posCutoff = await service.GetSettingAsync(pos.Id, "delivery.cutoff_time");
        var posNote = await service.GetSettingAsync(pos.Id, "delivery.require_note");
        ctx.Check(hanoiSet.Local.Mode == "override" && hanoiSet.Effective.Source?.Kind == "local"
            && posCutoff.Effective.Value?.GetString() == "17:00" && posCutoff.Effective.Source?.Kind == "ancestor" && posCutoff.Effective.Source.BranchId == hanoi.Id
            && posNote.Effective.Value?.GetBoolean() == true && posNote.Effective.Source?.BranchId == companyA.Id,
            "each key resolves from the nearest ancestor override (Hanoi for cutoff, Company A for the flag)");
        var b1Note = await service.GetSettingAsync(b1.Id, "delivery.require_note");
        var b1Cutoff = await service.GetSettingAsync(b1.Id, "delivery.cutoff_time");
        ctx.Check(b1Note.Effective.Value?.GetBoolean() == false && b1Note.Effective.Source?.BranchId == companyB.Id
            && b1Cutoff.Effective.Source?.Kind == "system_default" && b1Cutoff.Effective.Value?.GetString() == "16:00",
            "company B keeps its own false override and never reads company A");
        var posLines = await service.GetSettingAsync(pos.Id, "delivery.max_lines");
        ctx.Check(posLines.Effective.Value?.GetInt32() == 0 && posLines.Effective.Source?.Kind == "ancestor", "integer 0 is an override and is inherited");
        var list = await service.GetSettingsAsync(pos.Id, "operations");
        ctx.Check(list.Items.Count == 3 && list.Items.All(i => i.Etag.StartsWith("\"bs-" + pos.Id + "-")), "branch settings list returns every scalar key of the group with an entity tag");

        // Version guard and payload validation.
        var stale = posCutoff.Etag;
        await Set(pos.Id, "delivery.cutoff_time", "\"18:00\"");
        await ctx.Reject(() => service.UpdateAsync(pos.Id, "delivery.cutoff_time", new() { Mode = "override", Value = J("\"19:00\"") }, stale), BranchSettingService.StaleRevision);
        var fresh = (await service.GetSettingAsync(pos.Id, "delivery.cutoff_time")).Etag;
        await ctx.Reject(() => service.UpdateAsync(pos.Id, "delivery.cutoff_time", new() { Mode = "override", Value = J("\"19:00\"") }, null), BranchSettingService.RevisionRequired);
        await ctx.Reject(() => service.UpdateAsync(pos.Id, "delivery.cutoff_time", new() { Mode = "override", Value = J("null") }, fresh), BranchSettingService.InvalidValue);
        await ctx.Reject(() => service.UpdateAsync(pos.Id, "delivery.cutoff_time", new() { Mode = "inherit", Value = J("\"19:00\"") }, fresh), BranchSettingService.InvalidValue);
        var linesTag = (await service.GetSettingAsync(pos.Id, "delivery.max_lines")).Etag;
        await ctx.Reject(() => service.UpdateAsync(pos.Id, "delivery.max_lines", new() { Mode = "override", Value = J("\"3\"") }, linesTag), BranchSettingService.InvalidValue);
        await ctx.Reject(() => service.UpdateAsync(pos.Id, "delivery.unknown", new() { Mode = "inherit" }, fresh), BranchSettingService.NotFound);
        await ctx.Reject(() => service.GetSettingAsync(root.Id, "delivery.cutoff_time"), BranchSettingService.NotFound);
        await Set(pos.Id, "delivery.cutoff_time", null);

        // Impact preview: resetting Hanoi moves the point of sale from 17:00 (Hanoi) to 16:30 (Company A).
        var hanoiView = await service.GetSettingAsync(hanoi.Id, "delivery.cutoff_time");
        var impact = await service.PreviewAsync(hanoi.Id, "delivery.cutoff_time", new() { Mode = "inherit" }, hanoiView.Etag);
        var change = impact.Changes.SingleOrDefault();
        ctx.Check(!impact.Blocked && impact.AffectedBranchCount == 1 && change?.BranchId == pos.Id && change.Before.Value?.GetString() == "17:00"
            && change.After.Value?.GetString() == "16:30" && change.After.SourceKind == "ancestor" && impact.Blockers.Count == 0,
            "impact preview lists the descendant whose effective value changes");
        var shadowed = await service.PreviewAsync(companyA.Id, "delivery.cutoff_time", new() { Mode = "override", Value = J("\"15:00\"") },
            (await service.GetSettingAsync(companyA.Id, "delivery.cutoff_time")).Etag);
        ctx.Check(shadowed.AffectedBranchCount == 0, "a parent change hidden by a nearer override affects no descendant");
        var reset = await service.UpdateAsync(hanoi.Id, "delivery.cutoff_time", new() { Mode = "inherit", Reason = "Back to company time" }, hanoiView.Etag);
        ctx.Check(reset.Local.Mode == "inherit" && reset.Effective.Value?.GetString() == "16:30" && reset.Effective.Source?.BranchId == companyA.Id && reset.Etag != hanoiView.Etag,
            "reset to inherit removes the override and returns the inherited value with a new entity tag");
        var afterReset = await service.GetSettingAsync(pos.Id, "delivery.cutoff_time");
        ctx.Check(afterReset.Effective.Value?.GetString() == "16:30" && afterReset.Effective.Source?.BranchId == companyA.Id, "after the reset the point of sale inherits from Company A");

        // History rows are written with the change, newest first.
        var history = await service.GetHistoryAsync(hanoi.Id, "delivery.cutoff_time", 1, 20);
        ctx.Check(history.TotalCount == 2 && history.Items[0].NewLocal.Mode == "inherit" && history.Items[0].PreviousLocal.Value?.GetString() == "17:00"
            && history.Items[0].Revision == 2 && history.Items[0].Reason == "Back to company time" && history.Items[1].Reason == "Hanoi closes later"
            && history.Items[1].PreviousLocal.Mode == "inherit" && history.Items[1].ChangedBy == admin.UserId,
            "history records previous and new local state, trimmed reason, actor and revision");
        var countBefore = await db.BranchSettingHistories.CountAsync();
        await Set(companyA.Id, "delivery.cutoff_time", "\"16:30\"");
        ctx.Check(await db.BranchSettingHistories.CountAsync() == countBefore, "saving the same local value again writes no history");
        await Set(hanoi.Id, "delivery.cutoff_time", "\"17:00\"");
        ctx.Check((await service.GetHistoryAsync(hanoi.Id, "delivery.cutoff_time", 1, 20)).Items[0].Revision == 3, "a new override after a reset continues the revision");

        // Module-managed keys are written by their module only.
        db.SettingDefinitions.Add(new SettingDefinition { Key = "wms.bs_module_probe", GroupKey = "warehouse", ValueType = "string", StorageKind = SettingDefinition.Module, EditorKey = "wms.bs_module_probe" });
        await db.SaveChangesAsync();
        await ctx.Reject(() => service.UpdateAsync(hanoi.Id, "wms.bs_module_probe", new() { Mode = "inherit" }, "\"x\""), BranchSettingService.ModuleManaged);
        ctx.Check((await service.GetSettingsAsync(hanoi.Id, "warehouse")).Items.Count == 0, "a module key without a registered provider is not listed");

        // Data scope: edit only at Company A; view of the whole company except the point of sale for the source check.
        var companyEditor = new ScopedUser(id => true, id => id == companyA.Id);
        await using (var scoped = ctx.Db(companyEditor))
        {
            var scopedService = new BranchSettingService(scoped, companyEditor, []);
            var noteView = await scopedService.GetSettingAsync(companyA.Id, "delivery.max_lines");
            ctx.Check(noteView.CanEdit && !(await scopedService.GetSettingAsync(hanoi.Id, "delivery.max_lines")).CanEdit, "canEdit follows the edit scope of the unit");
            // Company A has no max_lines override; setting one changes nothing below (Hanoi overrides) and is allowed.
            await scopedService.UpdateAsync(companyA.Id, "delivery.max_lines", new() { Mode = "override", Value = J("7") }, noteView.Etag);
            var lines = await scopedService.GetSettingAsync(companyA.Id, "delivery.max_lines");
            await ctx.Reject(() => scopedService.UpdateAsync(hanoi.Id, "delivery.max_lines", new() { Mode = "inherit" }, lines.Etag), BranchSettingService.AccessDenied);
            // Hanoi and the point of sale inherit require_note from Company A, so changing it there reaches units outside the edit scope.
            var note = await scopedService.GetSettingAsync(companyA.Id, "delivery.require_note");
            await ctx.Reject(() => scopedService.PreviewAsync(companyA.Id, "delivery.require_note", new() { Mode = "override", Value = J("false") }, note.Etag), BranchSettingService.AccessDenied);
            await ctx.Reject(() => scopedService.UpdateAsync(companyA.Id, "delivery.require_note", new() { Mode = "override", Value = J("false") }, note.Etag), BranchSettingService.AccessDenied);
            ctx.Check((await scopedService.GetSettingAsync(pos.Id, "delivery.require_note")).Effective.Value?.GetBoolean() == true,
                "a write reaching descendants outside the edit scope is refused (403) and changes nothing");
        }
        var posViewer = new ScopedUser(id => id == pos.Id, _ => false);
        await using (var scoped = ctx.Db(posViewer))
        {
            var scopedService = new BranchSettingService(scoped, posViewer, []);
            var hidden = await scopedService.GetSettingAsync(pos.Id, "delivery.require_note");
            ctx.Check(hidden.Effective.Source?.Kind == "ancestor" && hidden.Effective.Source.BranchId == null && hidden.Effective.Source.BranchName == null && !hidden.CanEdit,
                "the inherited source unit is hidden from a caller who cannot view it");
            await ctx.Reject(() => scopedService.GetSettingAsync(hanoi.Id, "delivery.require_note"), BranchSettingService.NotFound);
            await ctx.Reject(() => scopedService.UpdateAsync(pos.Id, "delivery.require_note", new() { Mode = "inherit" }, hidden.Etag), BranchSettingService.AccessDenied);
        }

        // Inactive ancestors are skipped; deactivating Hanoi hands its children to Company A.
        // Writes clear the change tracker, so the flag is flipped in SQL.
        await db.Branches.Where(b => b.Id == hanoi.Id).ExecuteUpdateAsync(u => u.SetProperty(b => b.IsActive, false));
        var skipped = await service.GetSettingAsync(pos.Id, "delivery.cutoff_time");
        ctx.Check(skipped.Effective.Value?.GetString() == "16:30" && skipped.Effective.Source?.BranchId == companyA.Id,
            "an inactive ancestor is skipped by the resolver");
        await db.Branches.Where(b => b.Id == hanoi.Id).ExecuteUpdateAsync(u => u.SetProperty(b => b.IsActive, true));
    }

    /// <summary>Caller with per-branch view and edit decisions.</summary>
    private sealed class ScopedUser(Func<int, bool> view, Func<int, bool> edit) : ICurrentUser
    {
        public string? UserId => "branch-setting-verifier";
        public bool IsSystem => false;
        public bool IsAdmin => false;
        public int[] BranchIds => [];
        public bool HasUnrestrictedScope => true;
        public bool CanAt(string action, int branchId) => action == BranchSettingService.EditAction ? edit(branchId) : view(branchId);
    }
}
