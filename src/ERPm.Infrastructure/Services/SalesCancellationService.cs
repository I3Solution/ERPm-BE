using ERPm.Application.DTOs.Sales;
using ERPm.Domain.Entities.Accounting;
using ERPm.Domain.Entities.Inventory;
using Microsoft.EntityFrameworkCore;
using static ERPm.Infrastructure.Services.Common.PostingTransaction;

namespace ERPm.Infrastructure.Services;

public partial class SalesInvoiceService
{
    public Task<SalesInvoiceDto> CancelInvoiceAsync(int id, string reason, CancellationToken cancellationToken = default)
    {
        Require(!string.IsNullOrWhiteSpace(reason) && reason.Length <= 500, "error.validation.request");
        return posting.RunAsync(async () =>
        {
            await posting.LockAsync($"sale-cancel:{id}", cancellationToken);
            var invoice = await context.SalesInvoices.Include(i => i.Items).Include(i => i.StockIssue)
                .Include(i => i.JournalEntry!).ThenInclude(j => j.Lines).SingleOrDefaultAsync(i => i.Id == id, cancellationToken);
            Require(invoice != null && invoice.Status != VoucherStatus.Draft, "error.sales.cancelled", 409);
            if (invoice!.Status == VoucherStatus.Cancelled) return Map(invoice);
            Require(invoice.JournalEntry != null, "error.accounting.accounts_missing", 409);
            var goods = invoice.Items.Where(i => i.CostPrice > 0 || invoice.StockIssue != null).GroupBy(i => i.ItemId).OrderBy(g => g.Key);
            foreach (var group in goods) await posting.LockAsync($"stock:{invoice.WarehouseId}:{group.Key}", cancellationToken);
            if (invoice.CustomerId.HasValue) await posting.LockAsync($"partner:{invoice.CustomerId}", cancellationToken);
            var stockItems = invoice.StockIssueId.HasValue
                ? await context.StockIssueItems.Where(i => i.StockIssueId == invoice.StockIssueId).ToListAsync(cancellationToken) : [];
            foreach (var group in stockItems.GroupBy(i => i.ItemId))
            {
                var balance = await context.StockBalances.SingleOrDefaultAsync(b => b.WarehouseId == invoice.WarehouseId && b.ItemId == group.Key, cancellationToken);
                Require(balance != null, "error.stock.negative_balance", 409);
                var quantity = group.Sum(i => i.Quantity);
                balance!.InventoryValue += group.Sum(i => i.TotalCost);
                balance.AverageCostPrice = Round(balance.InventoryValue / (balance.QuantityOnHand + quantity));
                balance.QuantityOnHand += quantity;
            }
            if (invoice.CustomerId.HasValue && invoice.RemainingDebt > 0)
            {
                var customer = await context.Partners.SingleAsync(p => p.Id == invoice.CustomerId, cancellationToken);
                customer.DebtBalance -= invoice.RemainingDebt;
            }
            var reversal = new JournalEntry
            {
                VoucherNumber = await posting.NumberAsync("PKT", cancellationToken), VoucherType = "SALES_REVERSAL", BranchId = invoice.BranchId,
                ReversesJournalEntryId = invoice.JournalEntryId, Description = reason.Trim(), Status = VoucherStatus.Posted,
                Lines = invoice.JournalEntry!.Lines.Select(line => new JournalEntryLine
                {
                    AccountId = line.AccountId, PartnerId = line.PartnerId, DebitAmount = line.CreditAmount, CreditAmount = line.DebitAmount, Description = reason.Trim()
                }).ToList()
            };
            context.JournalEntries.Add(reversal);
            invoice.Status = VoucherStatus.Cancelled;
            invoice.CancelledAtUtc = DateTime.UtcNow;
            invoice.CancelledBy = user?.UserId;
            invoice.CancellationReason = reason.Trim();
            if (invoice.StockIssue != null) invoice.StockIssue.Status = VoucherStatus.Cancelled;
            context.AllowCancellation = true;
            try { await context.SaveChangesAsync(cancellationToken); }
            finally { context.AllowCancellation = false; }
            return Map(invoice);
        }, cancellationToken);
    }
}
