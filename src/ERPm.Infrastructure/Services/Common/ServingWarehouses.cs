using ERPm.Application.DTOs.Inventory;
using ERPm.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using static ERPm.Infrastructure.Services.Common.PostingTransaction;

namespace ERPm.Infrastructure.Services.Common;

/// <summary>Which warehouses a selling branch may ship its sales orders from.</summary>
internal static class ServingWarehouses
{
    /// <summary>
    /// The branch's configured list, or its own active warehouses when it has none. Only active warehouses of the
    /// branch's company count. Exactly one entry is the default and it comes first.
    /// </summary>
    public static async Task<List<ServingWarehouseDto>> ForBranchAsync(IApplicationDbContext context, int branchId, CancellationToken ct)
    {
        // Read outside the data scope: a seller sees the order's branch, not always the warehouse's.
        var companyId = await context.Branches.IgnoreQueryFilters().Where(b => b.Id == branchId).Select(b => b.CompanyId).SingleOrDefaultAsync(ct);
        var active = context.Warehouses.IgnoreQueryFilters().Where(w => !w.IsDeleted && w.IsActive && !w.Branch!.IsDeleted && w.Branch.IsActive);
        var configured = context.BranchServingWarehouses.IgnoreQueryFilters().Where(s => !s.IsDeleted && s.BranchId == branchId);
        var list = await configured.AnyAsync(ct)
            ? await configured.Join(active.Where(w => w.Branch!.CompanyId == companyId), s => s.WarehouseId, w => w.Id, (s, w) => new ServingWarehouseDto
                { WarehouseId = w.Id, Code = w.Code, Name = w.Name, BranchId = w.BranchId, BranchName = w.Branch!.Name, IsDefault = s.IsDefault, IsConfigured = true }).ToListAsync(ct)
            : await active.Where(w => w.BranchId == branchId).Select(w => new ServingWarehouseDto
                { WarehouseId = w.Id, Code = w.Code, Name = w.Name, BranchId = w.BranchId, BranchName = w.Branch!.Name, IsDefault = w.IsDefault }).ToListAsync(ct);
        var defaultId = (list.Where(w => w.IsDefault).MinBy(w => w.WarehouseId) ?? list.MinBy(w => w.WarehouseId))?.WarehouseId;
        foreach (var w in list) w.IsDefault = w.WarehouseId == defaultId;
        return list.OrderByDescending(w => w.IsDefault).ThenBy(w => w.Code).ToList();
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
}
