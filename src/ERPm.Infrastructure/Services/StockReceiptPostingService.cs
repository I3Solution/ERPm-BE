using ERPm.Application.Common.Interfaces;
using ERPm.Application.DTOs.Inventory;
using ERPm.Domain.Entities.Accounting;
using ERPm.Domain.Entities.Catalog;
using ERPm.Domain.Entities.Inventory;
using ERPm.Domain.Entities.Partner;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;
using static ERPm.Infrastructure.Services.Common.PostingTransaction;

namespace ERPm.Infrastructure.Services;

public sealed class StockReceiptPostingService(ApplicationDbContext context, PostingTransaction posting, ICurrentUser user)
{
    public Task<bool> CreateAsync(CreateStockReceiptRequest request, CancellationToken ct)
    {
        Require(request.RequestId != Guid.Empty && request.WarehouseId > 0 && request.Items is { Count: > 0 and <= 200 }, "error.validation.request");
        Require(request.IsOpeningBalance ? user.IsAdmin || user.IsSystem : request.SupplierId > 0, "error.inventory.supplier_required");
        Require(!request.IsOpeningBalance || request.SupplierId == null, "error.validation.request");
        foreach (var line in request.Items)
        {
            Require(line.ItemId > 0, "error.inventory.item");
            ValidateNumber(line.Quantity, true);
            ValidateNumber(line.UnitPrice, true);
        }
        var hash = Hash(request);
        return posting.RunAsync(async () =>
        {
            await posting.LockAsync($"receipt-request:{request.RequestId}", ct);
            var existing = await context.StockReceipts.IgnoreQueryFilters().SingleOrDefaultAsync(r => r.RequestId == request.RequestId, ct);
            if (existing != null)
            {
                Require(context.IsUnrestricted || context.AllowedBranchIds.Contains(existing.BranchId), "error.access.denied", 403);
                Require(existing.RequestHash == hash, "error.request.conflict", 409);
                return true;
            }
            var warehouse = await context.Warehouses.SingleOrDefaultAsync(w => w.Id == request.WarehouseId && w.IsActive && w.Branch!.IsActive, ct);
            Require(warehouse != null, "error.inventory.warehouse", 404);
            Require(request.BranchId == null || request.BranchId == warehouse!.BranchId, "error.inventory.branch_mismatch");
            var ids = request.Items.Select(i => i.ItemId).Distinct().Order().ToArray();
            foreach (var id in ids) await posting.LockSharedAsync($"item:{id}", ct);
            foreach (var id in ids) await posting.LockAsync($"stock:{warehouse!.Id}:{id}", ct);
            if (request.SupplierId.HasValue) await posting.LockAsync($"partner:{request.SupplierId}", ct);
            var items = await context.Items.Where(i => ids.Contains(i.Id) && i.IsActive && i.ItemType == ItemType.Goods).ToListAsync(ct);
            Require(items.Count == ids.Length, "error.inventory.item");
            var supplier = request.SupplierId.HasValue ? await context.Partners.SingleOrDefaultAsync(p => p.Id == request.SupplierId && p.IsActive, ct) : null;
            Require(request.IsOpeningBalance || supplier != null && supplier.PartnerType.HasFlag(PartnerType.Supplier), "error.inventory.supplier_required");
            var accounts = await posting.AccountsAsync(["156", request.IsOpeningBalance ? "411" : "331"], ct);
            var receipt = new StockReceipt
            {
                RequestId = request.RequestId, RequestHash = hash, BranchId = warehouse!.BranchId, WarehouseId = warehouse.Id,
                SupplierId = request.SupplierId, Code = await posting.NumberAsync("PNK", ct), Notes = request.Notes, Status = VoucherStatus.Posted
            };
            var balances = await context.StockBalances.Where(b => b.WarehouseId == warehouse.Id && ids.Contains(b.ItemId)).ToDictionaryAsync(b => b.ItemId, ct);
            foreach (var group in request.Items.GroupBy(i => i.ItemId))
            {
                var quantity = group.Sum(i => i.Quantity);
                var amount = group.Sum(i => Round(i.Quantity * i.UnitPrice));
                ValidateNumber(quantity, true);
                ValidateNumber(amount, true);
                foreach (var line in group) receipt.Items.Add(new StockReceiptItem { ItemId = line.ItemId, Quantity = line.Quantity, UnitPrice = line.UnitPrice, Amount = Round(line.Quantity * line.UnitPrice) });
                if (!balances.TryGetValue(group.Key, out var balance))
                {
                    balance = new StockBalance { WarehouseId = warehouse.Id, ItemId = group.Key };
                    context.StockBalances.Add(balance);
                    balances.Add(group.Key, balance);
                }
                Require(balance.QuantityOnHand >= 0, "error.stock.negative_balance", 409);
                var newQuantity = balance.QuantityOnHand + quantity;
                ValidateNumber(newQuantity, true);
                balance.InventoryValue += amount;
                ValidateNumber(balance.InventoryValue);
                balance.AverageCostPrice = Round(balance.InventoryValue / newQuantity);
                balance.QuantityOnHand = newQuantity;
            }
            receipt.TotalAmount = receipt.Items.Sum(i => i.Amount);
            ValidateNumber(receipt.TotalAmount, true);
            if (supplier != null) supplier.DebtBalance -= receipt.TotalAmount;
            receipt.JournalEntry = new JournalEntry
            {
                VoucherNumber = await posting.NumberAsync("PKT", ct), VoucherType = request.IsOpeningBalance ? "OPENING_STOCK" : "STOCK_RECEIPT",
                BranchId = warehouse.BranchId, Description = receipt.Code, Status = VoucherStatus.Posted,
                Lines = [
                    new JournalEntryLine { AccountId = accounts["156"], DebitAmount = receipt.TotalAmount, PartnerId = supplier?.Id },
                    new JournalEntryLine { AccountId = accounts[request.IsOpeningBalance ? "411" : "331"], CreditAmount = receipt.TotalAmount, PartnerId = supplier?.Id }
                ]
            };
            context.StockReceipts.Add(receipt);
            return true;
        }, ct);
    }
}
