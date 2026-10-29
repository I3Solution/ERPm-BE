using ERPm.Application.Common.Interfaces;
using ERPm.Application.DTOs.Inventory;
using ERPm.Application.Features.Inventory;
using ERPm.Application.Features.System;
using ERPm.Domain.Entities.Accounting;
using ERPm.Domain.Entities.Inventory;
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

    public async Task<List<StockBalanceDto>> GetStockBalancesAsync(int? warehouseId, int? branchId = null, CancellationToken cancellationToken = default)
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

        return await query
            .OrderBy(b => b.Item!.Code)
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
                AverageCostPrice = b.AverageCostPrice,
                TotalValue = b.InventoryValue,
                IsLowStock = b.Item != null && b.QuantityOnHand <= b.Item.MinStockAlert
            })
            .ToListAsync(cancellationToken);
    }

    public Task<bool> CreateStockReceiptAsync(CreateStockReceiptRequest request, CancellationToken cancellationToken = default)
        => _receipts.CreateAsync(request, cancellationToken);
}
