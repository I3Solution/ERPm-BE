using ERPm.Infrastructure.Services.Common;
using ERPm.Application.Common.Interfaces;
using ERPm.Application.DTOs.Inventory;
using ERPm.Application.Features.Inventory;
using ERPm.Application.Features.System;
using ERPm.Domain.Entities.Accounting;
using ERPm.Domain.Entities.Inventory;
using ERPm.Domain.Entities.Sales;
using ERPm.Domain.Entities.System;
using ERPm.Application.Common;
using ERPm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERPm.Infrastructure.Services;

public class StockService : IStockService
{
    private readonly IApplicationDbContext _context;
    private readonly IBranchService _branchService;
    private readonly StockReceiptPostingService _receipts;

    public StockService(IApplicationDbContext context, IBranchService branchService, StockReceiptPostingService receipts)
    {
        _context = context;
        _branchService = branchService;
        _receipts = receipts;
    }

    public async Task<List<WarehouseDto>> GetWarehousesAsync(int? branchId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Warehouses
            .Include(w => w.Branch)
            .Where(w => w.IsActive)
            .AsNoTracking();

        if (branchId.HasValue && branchId.Value > 0)
        {
            var targetBranchIds = await _branchService.GetBranchAndDescendantIdsAsync(branchId.Value, cancellationToken);
            query = query.Where(w => targetBranchIds.Contains(w.BranchId));
        }

        return await query
            .OrderByDescending(w => w.IsDefault)
            .Select(w => new WarehouseDto
            {
                Id = w.Id,
                BranchId = w.BranchId,
                BranchName = w.Branch != null ? w.Branch.Name : null,
                Code = w.Code,
                Name = w.Name,
                Translations = w.Translations,
                Address = w.Address,
                IsDefault = w.IsDefault
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<StockBalanceDto>> GetStockBalancesAsync(int? warehouseId, int? branchId = null, ERPm.Application.Common.Crud.ListSort? sort = null, CancellationToken cancellationToken = default)
    {
        var query = _context.StockBalances
            .Include(b => b.Warehouse)
            .Include(b => b.Item)
                .ThenInclude(i => i!.BaseUom)
            .AsNoTracking();

        if (warehouseId.HasValue && warehouseId.Value > 0)
        {
            query = query.Where(b => b.WarehouseId == warehouseId.Value);
        }

        if (branchId.HasValue && branchId.Value > 0)
        {
            var targetBranchIds = await _branchService.GetBranchAndDescendantIdsAsync(branchId.Value, cancellationToken);
            query = query.Where(b => b.Warehouse != null && targetBranchIds.Contains(b.Warehouse.BranchId));
        }

        var balances = query
            .Select(b => new StockBalanceDto
            {
                WarehouseId = b.WarehouseId,
                WarehouseName = b.Warehouse != null ? b.Warehouse.Name : string.Empty,
                ItemId = b.ItemId,
                ItemCode = b.Item != null ? b.Item.Code : string.Empty,
                ItemName = b.Item != null ? b.Item.Name : string.Empty,
                Barcode = b.Item != null ? b.Item.Barcode : null,
                UomName = b.Item != null && b.Item.BaseUom != null ? b.Item.BaseUom.Name : "Cái",
                QuantityOnHand = b.QuantityOnHand,
                QuantityReserved = b.QuantityReserved,
                QuantityAvailable = b.QuantityOnHand - b.QuantityReserved,
                AverageCostPrice = b.AverageCostPrice,
                TotalValue = b.InventoryValue,
                IsLowStock = b.Item != null && b.QuantityOnHand <= b.Item.MinStockAlert
            });
        // Without a requested column the list keeps its item-code order.
        return await (sort != null && DtoSorting.IsRequested(sort) ? balances.OrderByRequested(sort) : balances.OrderBy(b => b.ItemCode)).ToListAsync(cancellationToken);
    }

    public Task<bool> CreateStockReceiptAsync(CreateStockReceiptRequest request, CancellationToken cancellationToken = default)
        => _receipts.CreateAsync(request, cancellationToken);

    /// <summary>Legacy read: the effective list as an array. An invalid fixed list is refused so the caller can tell.</summary>
    public async Task<List<ServingWarehouseDto>> GetServingWarehousesAsync(int branchId, CancellationToken cancellationToken = default)
    {
        await RequireReadableAsync(branchId, cancellationToken);
        var resolution = await ServingWarehouses.PolicyAsync(_context, branchId, cancellationToken);
        PostingTransaction.Require(!resolution.Invalid, "error.sd.warehouse_not_serving", 409);
        return resolution.Warehouses;
    }

    public async Task<ServingPolicyView> GetServingPolicyAsync(int branchId, bool canEdit, CancellationToken cancellationToken = default)
    {
        await RequireReadableAsync(branchId, cancellationToken);
        var tree = await ServingWarehouses.LoadAsync(_context, branchId, cancellationToken);
        return await ViewAsync(tree, branchId, canEdit, cancellationToken);
    }

    public Task<ServingPolicyPreview> PreviewServingPolicyAsync(int branchId, UpdateServingWarehousesRequest request, CancellationToken cancellationToken = default)
        => ChangeServingPolicyAsync(branchId, request, null, cancellationToken);

    public Task<ServingPolicyView> SaveServingPolicyAsync(int branchId, UpdateServingWarehousesRequest request, CancellationToken cancellationToken = default)
    {
        PostingTransaction.Require(request.Mode != null, "error.branch_setting.invalid_value");
        var posting = new PostingTransaction((ApplicationDbContext)_context);
        return posting.RunAsync(async () =>
        {
            await ChangeServingPolicyAsync(branchId, request, posting, cancellationToken);
            return await ViewAsync(await ServingWarehouses.LoadAsync(_context, branchId, cancellationToken), branchId, true, cancellationToken);
        }, cancellationToken);
    }

    /// <summary>Legacy write: a list with rows is a fixed list, an empty list is the branch's own warehouses. Same guards as the new API.</summary>
    public Task<List<ServingWarehouseDto>> UpdateServingWarehousesAsync(int branchId, UpdateServingWarehousesRequest request, CancellationToken cancellationToken = default)
    {
        var posting = new PostingTransaction((ApplicationDbContext)_context);
        return posting.RunAsync(async () =>
        {
            await ChangeServingPolicyAsync(branchId, new UpdateServingWarehousesRequest { Warehouses = request.Warehouses ?? [] }, posting, cancellationToken);
            return await ServingWarehouses.ForBranchAsync(_context, branchId, cancellationToken);
        }, cancellationToken);
    }

    /// <summary>Not found unless the unit is in the caller's data scope.</summary>
    private async Task RequireReadableAsync(int branchId, CancellationToken ct)
        => PostingTransaction.Require(await _context.Branches.AnyAsync(b => b.Id == branchId, ct), "error.branch_setting.not_found", 404);

    private async Task<ServingPolicyView> ViewAsync(ServingWarehouses.Tree tree, int branchId, bool canEdit, CancellationToken ct)
    {
        var resolution = ServingWarehouses.Resolve(tree, branchId);
        var local = tree.Policies.GetValueOrDefault(branchId);
        var source = new ServingPolicySource { Kind = resolution.SourceKind(branchId) };
        // Name the source only when the caller may see that unit.
        if (resolution.SourceBranchId is int sourceId && await _context.Branches.AnyAsync(b => b.Id == sourceId, ct))
        { source.BranchId = sourceId; source.BranchName = tree.Nodes[sourceId].Name; }
        return new ServingPolicyView
        {
            BranchId = branchId,
            CanEdit = canEdit,
            Revision = local?.Revision ?? 0,
            Local = local == null ? new ServingPolicyLocal() : new ServingPolicyLocal
            {
                Mode = "override",
                Value = new ServingPolicyValue
                {
                    Mode = ModeName(local.Mode),
                    Warehouses = local.Mode == WarehousePolicyMode.FixedList
                        ? (tree.Lists.GetValueOrDefault(branchId) ?? []).Select(l => new ServingWarehouseLine { WarehouseId = l.WarehouseId, IsDefault = l.IsDefault }).ToList()
                        : null
                }
            },
            Effective = new ServingPolicyEffective
            {
                Status = resolution.Invalid ? "invalid" : "resolved",
                ErrorCode = resolution.Invalid ? "error.sd.warehouse_not_serving" : null,
                Mode = ModeName(resolution.Mode),
                Warehouses = resolution.Invalid ? [] : resolution.Warehouses,
                DefaultWarehouseId = resolution.DefaultId,
                Source = source
            }
        };
    }

    private static string ModeName(WarehousePolicyMode mode) => mode == WarehousePolicyMode.FixedList ? "fixed_list" : "own_warehouses";

    /// <summary>
    /// Works out what a save changes for the unit and every unit inheriting from it, refuses changes reaching units
    /// outside the caller's scope, and counts orders that hold stock in a warehouse their branch would lose.
    /// With <paramref name="posting"/> it also saves, under the company policy lock (order, then policy, then stock).
    /// </summary>
    private async Task<ServingPolicyPreview> ChangeServingPolicyAsync(int branchId, UpdateServingWarehousesRequest request, PostingTransaction? posting, CancellationToken ct)
    {
        var legacy = request.Mode == null;
        var lines = request.Warehouses ?? [];
        WarehousePolicyMode? mode = request.Mode switch
        {
            null => lines.Count > 0 ? WarehousePolicyMode.FixedList : WarehousePolicyMode.OwnWarehouses,
            "fixed_list" => WarehousePolicyMode.FixedList,
            "own_warehouses" => WarehousePolicyMode.OwnWarehouses,
            "inherit" => null,
            _ => throw new BusinessException("error.branch_setting.invalid_value", 400)
        };
        var invalidCode = legacy ? "error.validation.request" : "error.branch_setting.invalid_value";
        if (mode == WarehousePolicyMode.FixedList)
            PostingTransaction.Require(lines.Count is > 0 and <= 100 && lines.Select(l => l.WarehouseId).Distinct().Count() == lines.Count
                && lines.Count(l => l.IsDefault) == 1, invalidCode);
        else
            PostingTransaction.Require(lines.Count == 0, invalidCode);
        if (!legacy) PostingTransaction.Require(!string.IsNullOrWhiteSpace(request.Reason) && request.Reason.Trim().Length <= 500, "error.branch_setting.reason_required");

        var target = await _context.Branches.AnyAsync(b => b.Id == branchId && b.IsActive
            && b.OrgUnitType != OrgUnitType.Warehouse, ct);   // A single-company install keeps its list on the root group, as in S27.1.
        PostingTransaction.Require(target, "error.branch_setting.not_found", 404);
        if (posting != null) await posting.LockAsync(await ServingWarehouses.LockKeyAsync(_context, branchId, ct), ct);

        var tree = await ServingWarehouses.LoadAsync(_context, branchId, ct);
        PostingTransaction.Require(lines.All(l => tree.Warehouses.ContainsKey(l.WarehouseId)), "error.sd.warehouse_not_serving", 409);
        var units = tree.Subtree(branchId).Where(id => tree.Nodes[id].IsActive || id == branchId).ToList();
        var before = units.ToDictionary(id => id, id => ServingWarehouses.Resolve(tree, id));
        if (mode is WarehousePolicyMode m) tree.Policies[branchId] = new ServingWarehouses.Policy(m, 0); else tree.Policies.Remove(branchId);
        tree.Lists[branchId] = mode == WarehousePolicyMode.FixedList ? lines.Select(l => new ServingWarehouses.Line(l.WarehouseId, l.IsDefault)).ToList() : [];
        var after = units.ToDictionary(id => id, id => ServingWarehouses.Resolve(tree, id));

        var changed = units.Where(id => !before[id].UsableIds.SequenceEqual(after[id].UsableIds) || before[id].DefaultId != after[id].DefaultId
            || before[id].SourceBranchId != after[id].SourceBranchId || before[id].Invalid != after[id].Invalid).ToList();
        var scoped = (ApplicationDbContext)_context;
        PostingTransaction.Require(scoped.IsUnrestricted || changed.All(id => scoped.AllowedBranchIds.Contains(id)), "error.access.denied", 403);

        // Orders that hold stock in a warehouse their branch loses; a new default within the same list loses nothing.
        var lost = changed.Select(id => (Id: id, Lost: before[id].UsableIds.Except(after[id].UsableIds).ToArray()))
            .Where(p => p.Lost.Length > 0).ToDictionary(p => p.Id, p => p.Lost);
        var holding = 0;
        if (lost.Count > 0)
        {
            var branchIds = lost.Keys.ToArray();
            var warehouseIds = lost.Values.SelectMany(w => w).Distinct().ToArray();
            var held = await _context.StockReservations.IgnoreQueryFilters()
                .Where(r => r.Status == ReservationRecordStatus.Active && warehouseIds.Contains(r.WarehouseId))
                .Join(_context.SalesOrders.IgnoreQueryFilters().Where(o => !o.IsDeleted && branchIds.Contains(o.BranchId)), r => r.SalesOrderId, o => o.Id, (r, o) => new { o.Id, o.BranchId, r.WarehouseId })
                .Distinct().ToListAsync(ct);
            holding = held.Where(h => lost[h.BranchId].Contains(h.WarehouseId)).Select(h => h.Id).Distinct().Count();
        }
        var preview = new ServingPolicyPreview
        {
            Blocked = holding > 0,
            AffectedBranchCount = changed.Count,
            Changes = changed.Select(id => new ServingPolicyChange { BranchId = id, BranchName = tree.Nodes[id].Name, Before = Side(before[id], id), After = Side(after[id], id) }).ToList(),
            Blockers = holding > 0 ? [new ServingPolicyBlocker { Code = "error.branch_setting.active_reservations", Count = holding }] : []
        };
        if (posting == null) return preview;
        PostingTransaction.Require(holding == 0, "error.branch_setting.active_reservations", 409);

        var policy = await _context.BranchWarehousePolicies.SingleOrDefaultAsync(p => p.BranchId == branchId, ct);
        if (mode is not WarehousePolicyMode newMode) { if (policy != null) policy.IsDeleted = true; }
        else if (policy == null) _context.BranchWarehousePolicies.Add(new BranchWarehousePolicy { BranchId = branchId, Mode = newMode });
        else { policy.Mode = newMode; policy.Revision++; }

        // Only a fixed list keeps rows, so S27.1 code reads the same lists after a rollback.
        var keep = mode == WarehousePolicyMode.FixedList ? lines.Select(l => l.WarehouseId).ToArray() : [];
        var current = await _context.BranchServingWarehouses.Where(s => s.BranchId == branchId).ToListAsync(ct);
        // Clear the old default first: the one-default index is checked row by row.
        foreach (var row in current) { row.IsDefault = false; if (!keep.Contains(row.WarehouseId)) row.IsDeleted = true; }
        await _context.SaveChangesAsync(ct);
        foreach (var line in keep.Length > 0 ? lines : [])
        {
            var row = current.FirstOrDefault(r => r.WarehouseId == line.WarehouseId && !r.IsDeleted);
            if (row == null) _context.BranchServingWarehouses.Add(row = new BranchServingWarehouse { BranchId = branchId, WarehouseId = line.WarehouseId });
            row.IsDefault = line.IsDefault;
        }
        await _context.SaveChangesAsync(ct);
        return preview;
    }

    private static ServingPolicySide Side(ServingWarehouses.Resolution r, int branchId) => new()
    { WarehouseIds = r.UsableIds.ToList(), DefaultWarehouseId = r.DefaultId, SourceKind = r.SourceKind(branchId), Status = r.Invalid ? "invalid" : "resolved" };

    public async Task<List<StockAvailabilityDto>> GetAvailabilityAsync(int branchId, int warehouseId, IReadOnlyCollection<int> itemIds, CancellationToken cancellationToken = default)
    {
        PostingTransaction.Require(itemIds.Count is > 0 and <= 200, "error.validation.request");
        await ServingWarehouses.RequireAsync(_context, branchId, warehouseId, cancellationToken);
        var balances = await _context.StockBalances.IgnoreQueryFilters().AsNoTracking()
            .Where(b => !b.IsDeleted && b.WarehouseId == warehouseId && itemIds.Contains(b.ItemId))
            .ToDictionaryAsync(b => b.ItemId, cancellationToken);
        // An item never received in the warehouse has no balance row and nothing available.
        return itemIds.Distinct().Select(id => balances.TryGetValue(id, out var b)
            ? new StockAvailabilityDto { ItemId = id, QuantityOnHand = b.QuantityOnHand, QuantityReserved = b.QuantityReserved, QuantityAvailable = b.QuantityOnHand - b.QuantityReserved }
            : new StockAvailabilityDto { ItemId = id }).ToList();
    }
}
