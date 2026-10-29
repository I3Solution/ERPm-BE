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

    public StockService(IApplicationDbContext context, IBranchService branchService)
    {
        _context = context;
        _branchService = branchService;
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
                IsLowStock = b.Item != null && b.QuantityOnHand <= b.Item.MinStockAlert
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> CreateStockReceiptAsync(CreateStockReceiptRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Items.Count == 0) return false;

        var count = await _context.StockReceipts.CountAsync(cancellationToken);
        var code = $"PNK{count + 1:D5}";

        // Determine the receiving branch for the stock receipt
        var branchId = request.BranchId;
        if (!branchId.HasValue || branchId <= 0)
        {
            var wh = await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken);
            if (wh != null && wh.BranchId > 0)
            {
                branchId = wh.BranchId;
            }
            else
            {
                var rootBranch = await _context.Branches.FirstOrDefaultAsync(b => b.Code == "root" || b.IsDefault, cancellationToken);
                branchId = rootBranch?.Id ?? 1;
            }
        }

        var receipt = new StockReceipt
        {
            Code = code,
            ReceiptDate = DateTime.UtcNow,
            BranchId = branchId.Value,
            WarehouseId = request.WarehouseId,
            SupplierId = request.SupplierId,
            Status = VoucherStatus.Posted,
            Notes = request.Notes
        };

        decimal totalAmount = 0;

        foreach (var itemReq in request.Items)
        {
            var amount = itemReq.Quantity * itemReq.UnitPrice;
            totalAmount += amount;

            receipt.Items.Add(new StockReceiptItem
            {
                ItemId = itemReq.ItemId,
                Quantity = itemReq.Quantity,
                UnitPrice = itemReq.UnitPrice,
                Amount = amount
            });

            // Update or create StockBalance
            var balance = await _context.StockBalances
                .FirstOrDefaultAsync(b => b.WarehouseId == request.WarehouseId && b.ItemId == itemReq.ItemId, cancellationToken);

            if (balance != null)
            {
                var newQty = balance.QuantityOnHand + itemReq.Quantity;
                if (newQty > 0)
                {
                    // Weighted average cost
                    balance.AverageCostPrice = ((balance.QuantityOnHand * balance.AverageCostPrice) + amount) / newQty;
                }
                balance.QuantityOnHand = newQty;
            }
            else
            {
                balance = new StockBalance
                {
                    WarehouseId = request.WarehouseId,
                    ItemId = itemReq.ItemId,
                    QuantityOnHand = itemReq.Quantity,
                    AverageCostPrice = itemReq.UnitPrice
                };
                await _context.StockBalances.AddAsync(balance, cancellationToken);
            }
        }

        receipt.TotalAmount = totalAmount;
        await _context.StockReceipts.AddAsync(receipt, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }
}
