using ERPm.Infrastructure.Services.Common;
using ERPm.Application.Common.Interfaces;
using ERPm.Application.DTOs.Inventory;
using ERPm.Application.Features.Inventory;
using ERPm.Application.Features.System;
using ERPm.Domain.Entities.Accounting;
using ERPm.Domain.Entities.Inventory;
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

    public Task<List<ServingWarehouseDto>> GetServingWarehousesAsync(int branchId, CancellationToken cancellationToken = default)
        => ServingWarehouses.ForBranchAsync(_context, branchId, cancellationToken);

    public Task<List<ServingWarehouseDto>> UpdateServingWarehousesAsync(int branchId, UpdateServingWarehousesRequest request, CancellationToken cancellationToken = default)
    {
        var lines = request.Warehouses ?? [];
        PostingTransaction.Require(lines.Count <= 100 && lines.Select(l => l.WarehouseId).Distinct().Count() == lines.Count
            && (lines.Count == 0 || lines.Count(l => l.IsDefault) == 1), "error.validation.request");
        var db = (ApplicationDbContext)_context;
        var posting = new PostingTransaction(db);
        return posting.RunAsync(async () =>
        {
            await posting.LockAsync($"serving:{branchId}", cancellationToken);
            var branch = await db.Branches.IgnoreQueryFilters().SingleOrDefaultAsync(b => b.Id == branchId && !b.IsDeleted && b.IsActive, cancellationToken);
            PostingTransaction.Require(branch != null, "error.validation.request", 404);
            var ids = lines.Select(l => l.WarehouseId).ToArray();
            var valid = await db.Warehouses.IgnoreQueryFilters().CountAsync(w => ids.Contains(w.Id) && !w.IsDeleted && w.IsActive
                && !w.Branch!.IsDeleted && w.Branch.IsActive && w.Branch.CompanyId == branch!.CompanyId, cancellationToken);
            PostingTransaction.Require(valid == ids.Length, "error.sd.warehouse_not_serving", 409);

            var current = await db.BranchServingWarehouses.Where(s => s.BranchId == branchId).ToListAsync(cancellationToken);
            // Clear the old default first: the one-default index is checked row by row.
            foreach (var row in current) { row.IsDefault = false; if (!ids.Contains(row.WarehouseId)) row.IsDeleted = true; }
            await db.SaveChangesAsync(cancellationToken);
            foreach (var line in lines)
            {
                var row = current.FirstOrDefault(r => r.WarehouseId == line.WarehouseId && !r.IsDeleted);
                if (row == null) db.BranchServingWarehouses.Add(row = new BranchServingWarehouse { BranchId = branchId, WarehouseId = line.WarehouseId });
                row.IsDefault = line.IsDefault;
            }
            await db.SaveChangesAsync(cancellationToken);
            return await ServingWarehouses.ForBranchAsync(db, branchId, cancellationToken);
        }, cancellationToken);
    }

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
