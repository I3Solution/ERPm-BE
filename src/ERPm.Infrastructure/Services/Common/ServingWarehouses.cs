using ERPm.Application.DTOs.Inventory;
using ERPm.Application.Common.Interfaces;
using ERPm.Domain.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using static ERPm.Infrastructure.Services.Common.PostingTransaction;

namespace ERPm.Infrastructure.Services.Common;

/// <summary>
/// Which warehouses a selling branch may ship its sales orders from (key <c>wms.so_warehouse_policy</c>).
/// The policy is taken from the branch, else from its nearest active ancestor of the same company, else the branch's
/// own warehouses. A fixed list and its default always come from the same unit.
/// </summary>
internal static class ServingWarehouses
{
    internal sealed record Node(int Id, int? ParentId, string Name, bool IsActive);
    internal sealed record Policy(WarehousePolicyMode Mode, int Revision);
    internal sealed record Line(int WarehouseId, bool IsDefault);

    /// <summary>The units, policies, lists and active warehouses of one company, read outside the data scope.</summary>
    internal sealed class Tree
    {
        public int? CompanyId { get; init; }
        public Dictionary<int, Node> Nodes { get; init; } = [];
        public Dictionary<int, Policy> Policies { get; init; } = [];
        public Dictionary<int, List<Line>> Lists { get; init; } = [];
        public Dictionary<int, ServingWarehouseDto> Warehouses { get; init; } = [];

        /// <summary>The unit and every unit below it in the company.</summary>
        public List<int> Subtree(int branchId)
        {
            var result = new List<int> { branchId };
            for (var i = 0; i < result.Count; i++)
                result.AddRange(Nodes.Values.Where(n => n.ParentId == result[i] && n.Id != result[i]).Select(n => n.Id).Order());
            return result;
        }
    }

    /// <summary>Effective policy of one unit. Invalid means a fixed list with a warehouse that no longer serves or no single default.</summary>
    internal sealed record Resolution(int? SourceBranchId, WarehousePolicyMode Mode, bool Invalid, List<ServingWarehouseDto> Warehouses)
    {
        public string SourceKind(int branchId) => SourceBranchId == null ? "module_fallback" : SourceBranchId == branchId ? "local" : "ancestor";
        public int[] UsableIds => Invalid ? [] : Warehouses.Select(w => w.WarehouseId).Order().ToArray();
        public int? DefaultId => Invalid ? null : Warehouses.FirstOrDefault(w => w.IsDefault)?.WarehouseId;
    }

    // shortcut: loads the whole company per call; fine for tens of units and warehouses, cache per request if companies grow large.
    public static async Task<Tree> LoadAsync(IApplicationDbContext context, int branchId, CancellationToken ct)
    {
        // Read outside the data scope: a seller sees the order's branch, not always the warehouse's or the ancestor's.
        var companyId = await context.Branches.IgnoreQueryFilters().Where(b => b.Id == branchId).Select(b => b.CompanyId).SingleOrDefaultAsync(ct);
        var nodes = await context.Branches.IgnoreQueryFilters()
            .Where(b => !b.IsDeleted && (b.Id == branchId || (companyId != null && b.CompanyId == companyId)))
            .Select(b => new Node(b.Id, b.ParentId, b.Name, b.IsActive)).ToDictionaryAsync(n => n.Id, ct);
        var ids = nodes.Keys.ToArray();
        var policies = await context.BranchWarehousePolicies.IgnoreQueryFilters().Where(p => !p.IsDeleted && ids.Contains(p.BranchId))
            .ToDictionaryAsync(p => p.BranchId, p => new Policy(p.Mode, p.Revision), ct);
        var lists = (await context.BranchServingWarehouses.IgnoreQueryFilters().Where(s => !s.IsDeleted && ids.Contains(s.BranchId))
            .Select(s => new { s.BranchId, s.WarehouseId, s.IsDefault }).ToListAsync(ct))
            .GroupBy(s => s.BranchId).ToDictionary(g => g.Key, g => g.Select(s => new Line(s.WarehouseId, s.IsDefault)).ToList());
        var warehouses = await context.Warehouses.IgnoreQueryFilters()
            .Where(w => !w.IsDeleted && w.IsActive && !w.Branch!.IsDeleted && w.Branch.IsActive && w.Branch.CompanyId == companyId)
            .Select(w => new ServingWarehouseDto { WarehouseId = w.Id, Code = w.Code, Name = w.Name, BranchId = w.BranchId, BranchName = w.Branch!.Name, IsDefault = w.IsDefault })
            .ToDictionaryAsync(w => w.WarehouseId, ct);
        return new Tree { CompanyId = companyId, Nodes = nodes, Policies = policies, Lists = lists, Warehouses = warehouses };
    }

    public static Resolution Resolve(Tree tree, int branchId)
    {
        int? sourceId = null;
        for (int? id = branchId; id is int current && tree.Nodes.TryGetValue(current, out var node); id = node.ParentId)
        {
            if (node.IsActive && tree.Policies.ContainsKey(current)) { sourceId = current; break; }
            if (node.ParentId == current) break;
        }
        var mode = sourceId is int s ? tree.Policies[s].Mode : WarehousePolicyMode.OwnWarehouses;

        List<ServingWarehouseDto> list;
        var invalid = false;
        if (mode == WarehousePolicyMode.FixedList)
        {
            var lines = tree.Lists.GetValueOrDefault(sourceId!.Value) ?? [];
            invalid = lines.Count == 0 || lines.Any(l => !tree.Warehouses.ContainsKey(l.WarehouseId)) || lines.Count(l => l.IsDefault) != 1;
            list = lines.Where(l => tree.Warehouses.ContainsKey(l.WarehouseId))
                .Select(l => Copy(tree.Warehouses[l.WarehouseId], l.IsDefault, true)).ToList();
        }
        else
        {
            // The order's own branch, whichever unit set the mode; its default warehouse, else the lowest ID (as in S27.1).
            var own = tree.Warehouses.Values.Where(w => w.BranchId == branchId).ToList();
            var defaultId = (own.Where(w => w.IsDefault).MinBy(w => w.WarehouseId) ?? own.MinBy(w => w.WarehouseId))?.WarehouseId;
            list = own.Select(w => Copy(w, w.WarehouseId == defaultId, false)).ToList();
        }
        return new Resolution(sourceId, mode, invalid, list.OrderByDescending(w => w.IsDefault).ThenBy(w => w.Code).ToList());
    }

    private static ServingWarehouseDto Copy(ServingWarehouseDto w, bool isDefault, bool configured) => new()
    { WarehouseId = w.WarehouseId, Code = w.Code, Name = w.Name, BranchId = w.BranchId, BranchName = w.BranchName, IsDefault = isDefault, IsConfigured = configured };

    public static async Task<Resolution> PolicyAsync(IApplicationDbContext context, int branchId, CancellationToken ct)
        => Resolve(await LoadAsync(context, branchId, ct), branchId);

    /// <summary>
    /// The warehouses the branch may ship from, default first; empty when its fixed list is invalid.
    /// <see cref="ServingWarehouseDto.IsConfigured"/> is true when the effective policy is a fixed list.
    /// </summary>
    public static async Task<List<ServingWarehouseDto>> ForBranchAsync(IApplicationDbContext context, int branchId, CancellationToken ct)
    {
        var resolution = await PolicyAsync(context, branchId, ct);
        return resolution.Invalid ? [] : resolution.Warehouses;
    }

    /// <summary>Rejects a warehouse that is not active, not in the branch's company or not on its list.</summary>
    public static async Task RequireAsync(IApplicationDbContext context, int branchId, int warehouseId, CancellationToken ct)
        => Require((await ForBranchAsync(context, branchId, ct)).Any(w => w.WarehouseId == warehouseId), "error.sd.warehouse_not_serving", 409);

    /// <summary>The order's warehouse, or the branch default for an order that names none (orders made before S27.1).</summary>
    public static async Task<int> ResolveAsync(IApplicationDbContext context, int branchId, int? warehouseId, CancellationToken ct)
    {
        if (warehouseId is int id) { await RequireAsync(context, branchId, id, ct); return id; }
        var fallback = (await ForBranchAsync(context, branchId, ct)).FirstOrDefault();
        Require(fallback != null, "error.sd.warehouse_not_serving", 409);
        return fallback!.WarehouseId;
    }

    /// <summary>Lock key shared by policy writes (exclusive) and reservations (shared): order, then this, then stock.</summary>
    public static async Task<string> LockKeyAsync(IApplicationDbContext context, int branchId, CancellationToken ct)
        => $"serving:company:{await context.Branches.IgnoreQueryFilters().Where(b => b.Id == branchId).Select(b => b.CompanyId ?? b.Id).SingleOrDefaultAsync(ct)}";
}
