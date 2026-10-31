using ERPm.Application.DTOs.Sales;
using ERPm.Application.Common.Interfaces;
using ERPm.Application.Features.Sales;
using ERPm.Application.Features.System;
using ERPm.Domain.Entities.Accounting;
using ERPm.Domain.Entities.Catalog;
using ERPm.Domain.Entities.Inventory;
using ERPm.Domain.Entities.Partner;
using ERPm.Domain.Entities.Sales;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;
using static ERPm.Infrastructure.Services.Common.PostingTransaction;

namespace ERPm.Infrastructure.Services;

public partial class SalesInvoiceService(ApplicationDbContext context, IBranchService branches, PostingTransaction posting, ICurrentUser? user = null, ERPm.Application.Common.Notifications.INotificationEngine? notificationEngine = null) : ISalesInvoiceService
{
    public async Task<List<SalesInvoiceDto>> GetInvoicesAsync(int? branchId = null, CancellationToken cancellationToken = default)
    {
        var query = context.SalesInvoices.AsNoTracking();
        if (branchId > 0)
        {
            var ids = await branches.GetBranchAndDescendantIdsAsync(branchId.Value, cancellationToken);
            query = query.Where(i => ids.Contains(i.BranchId));
        }
        var invoices = await query.Include(i => i.Branch).Include(i => i.Items).ThenInclude(i => i.Item)
            .OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.Id).Take(50).ToListAsync(cancellationToken);
        return invoices.Select(Map).ToList();
    }

    public Task<SalesInvoiceDto> CreateInvoiceAsync(CreateSalesInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        Require(request.RequestId != Guid.Empty && request.WarehouseId > 0 && request.Items is { Count: > 0 and <= 200 }, "error.validation.request");
        Require(Enum.IsDefined(request.PaymentMethod), "error.validation.payment_method");
        ValidateNumber(request.DiscountAmount);
        ValidateNumber(request.PaidAmount);
        foreach (var line in request.Items)
        {
            Require(line.ItemId > 0 && line.DiscountPercent is >= 0 and <= 100, "error.validation.request");
            ValidateNumber(line.Quantity, true);
            ValidateNumber(line.UnitPrice);
            ValidateNumber(line.DiscountPercent);
        }
        var hash = Hash(request);
        return posting.RunAsync(async () =>
        {
            await posting.LockAsync($"sale-request:{request.RequestId}", cancellationToken);
            var existing = await context.SalesInvoices.IgnoreQueryFilters().FirstOrDefaultAsync(i => i.RequestId == request.RequestId, cancellationToken);
            if (existing != null)
            {
                Require(context.IsUnrestricted || context.AllowedBranchIds.Contains(existing.BranchId), "error.access.denied", 403);
                Require(existing.RequestHash == hash, "error.request.conflict", 409);
                return Map(await LoadAsync(existing.Id, cancellationToken));
            }
            var warehouse = await context.Warehouses.SingleOrDefaultAsync(w => w.Id == request.WarehouseId && w.IsActive && w.Branch!.IsActive, cancellationToken);
            Require(warehouse != null, "error.inventory.warehouse", 404);
            Require(request.BranchId == null || request.BranchId == warehouse!.BranchId, "error.inventory.branch_mismatch");
            var itemIds = request.Items.Select(i => i.ItemId).Distinct().Order().ToArray();
            foreach (var itemId in itemIds) await posting.LockSharedAsync($"item:{itemId}", cancellationToken);
            foreach (var itemId in itemIds) await posting.LockAsync($"stock:{warehouse!.Id}:{itemId}", cancellationToken);
            if (request.CustomerId.HasValue) await posting.LockAsync($"partner:{request.CustomerId}", cancellationToken);
            var items = await context.Items.Include(i => i.BaseUom).Where(i => itemIds.Contains(i.Id) && i.IsActive).ToDictionaryAsync(i => i.Id, cancellationToken);
            Require(items.Count == itemIds.Length && items.Values.All(i => i.ItemType != ItemType.Combo), "error.inventory.item");
            var balances = await context.StockBalances.Where(b => b.WarehouseId == warehouse!.Id && itemIds.Contains(b.ItemId)).ToDictionaryAsync(b => b.ItemId, cancellationToken);
            foreach (var group in request.Items.GroupBy(i => i.ItemId).Where(g => items[g.Key].ItemType == ItemType.Goods))
                Require(balances.TryGetValue(group.Key, out var balance) && balance.QuantityOnHand >= group.Sum(i => i.Quantity), "error.stock.insufficient", 409);

            var invoice = new SalesInvoice
            {
                RequestId = request.RequestId, RequestHash = hash,
                InvoiceNumber = await posting.NumberAsync("HD", cancellationToken), BranchId = warehouse!.BranchId,
                WarehouseId = warehouse.Id, CustomerId = request.CustomerId, PaymentMethod = request.PaymentMethod,
                CustomerName = request.CustomerName.Trim(), DiscountAmount = request.DiscountAmount, Notes = request.Notes, Status = VoucherStatus.Posted
            };
            var issue = new StockIssue
            {
                Code = await posting.NumberAsync("PXK", cancellationToken), BranchId = warehouse.BranchId,
                WarehouseId = warehouse.Id, CustomerId = request.CustomerId, Status = VoucherStatus.Posted, Reason = invoice.InvoiceNumber
            };
            foreach (var line in request.Items)
            {
                var item = items[line.ItemId];
                Require(user == null || user.IsSystem || user.IsAdmin || line.UnitPrice == item.SalePrice, "error.sales.price_override", 403);
                var cost = item.ItemType == ItemType.Goods ? balances[item.Id].AverageCostPrice : 0m;
                var costAmount = item.ItemType == ItemType.Goods ?
                    (balances[item.Id].QuantityOnHand == line.Quantity ? balances[item.Id].InventoryValue : Math.Min(balances[item.Id].InventoryValue, Round(line.Quantity * cost))) : 0m;
                var amount = Round(line.Quantity * line.UnitPrice * (1 - line.DiscountPercent / 100));
                invoice.Items.Add(new SalesInvoiceItem
                {
                    ItemId = item.Id, ItemCode = item.Code, ItemName = item.Name, UnitName = item.BaseUom?.Name,
                    Quantity = line.Quantity, UnitPrice = line.UnitPrice, DiscountPercent = line.DiscountPercent, Amount = amount, CostPrice = cost, CostAmount = costAmount
                });
                if (item.ItemType == ItemType.Goods)
                {
                    issue.Items.Add(new StockIssueItem { ItemId = item.Id, Quantity = line.Quantity, UnitCost = cost, TotalCost = costAmount });
                    balances[item.Id].QuantityOnHand -= line.Quantity;
                    balances[item.Id].InventoryValue -= costAmount;

                    if (notificationEngine != null && item.MinStockAlert > 0 && balances[item.Id].QuantityOnHand <= item.MinStockAlert)
                    {
                        _ = notificationEngine.PublishAsync(new ERPm.Domain.Events.NotificationEvent(
                            "WMS_LOW_STOCK",
                            ERPm.Domain.Enums.NotificationCategory.WMS,
                            ERPm.Domain.Enums.NotificationSeverity.WARNING,
                            ERPm.Domain.Enums.NotificationTargetType.BranchTree,
                            branchId: warehouse.BranchId,
                            parameters: new Dictionary<string, object>
                            {
                                ["item_name"] = item.Name,
                                ["item_code"] = item.Code,
                                ["warehouse_name"] = warehouse.Name,
                                ["current_stock"] = balances[item.Id].QuantityOnHand,
                                ["min_stock"] = item.MinStockAlert,
                                ["uom"] = item.BaseUom?.Name ?? "cái"
                            },
                            actionUrl: $"/inventory?search={item.Code}"
                        ));
                    }
                }
            }
            invoice.SubTotal = invoice.Items.Sum(i => i.Amount);
            Require(request.DiscountAmount <= invoice.SubTotal, "error.sales.discount");
            invoice.TotalAmount = invoice.SubTotal - request.DiscountAmount;
            ValidateNumber(invoice.TotalAmount, true);
            Require(request.PaymentMethod != PaymentMethod.Debt || request.PaidAmount == 0, "error.validation.payment_method");
            Require(request.PaymentMethod != PaymentMethod.BankTransfer || request.PaidAmount <= invoice.TotalAmount, "error.sales.overpayment");
            invoice.TenderedAmount = request.PaidAmount;
            invoice.PaidAmount = Math.Min(request.PaidAmount, invoice.TotalAmount);
            invoice.ChangeAmount = request.PaidAmount - invoice.PaidAmount;
            invoice.RemainingDebt = invoice.TotalAmount - invoice.PaidAmount;
            ERPm.Domain.Entities.Partner.Partner? customer = null;
            if (request.CustomerId.HasValue)
            {
                customer = await context.Partners.SingleOrDefaultAsync(p => p.Id == request.CustomerId && p.IsActive, cancellationToken);
                Require(customer != null && customer.PartnerType.HasFlag(PartnerType.Customer), "error.sales.customer");
                invoice.CustomerName = customer!.Name;
            }
            if (invoice.RemainingDebt > 0)
            {
                Require(customer != null, "error.sales.customer_required");
                var receivables = await context.SalesInvoices.IgnoreQueryFilters().Where(i => !i.IsDeleted && i.Status == VoucherStatus.Posted && i.CustomerId == request.CustomerId)
                    .SumAsync(i => i.RemainingDebt, cancellationToken);
                Require(customer!.CreditLimit >= receivables + invoice.RemainingDebt, "error.sales.credit_limit", 409);
                customer.DebtBalance += invoice.RemainingDebt;
            }
            issue.TotalCostAmount = issue.Items.Sum(i => i.TotalCost);
            ValidateNumber(issue.TotalCostAmount);
            if (issue.Items.Count > 0) invoice.StockIssue = issue;
            var codes = new List<string> { "511" };
            var cashAccount = request.PaymentMethod == PaymentMethod.BankTransfer ? "112" : "111";
            if (invoice.PaidAmount > 0) codes.Add(cashAccount);
            if (invoice.RemainingDebt > 0) codes.Add("131");
            if (issue.TotalCostAmount > 0) codes.AddRange(["632", "156"]);
            var accounts = await posting.AccountsAsync(codes, cancellationToken);
            var journal = new JournalEntry
            {
                VoucherNumber = await posting.NumberAsync("PKT", cancellationToken), VoucherType = "SALES_INVOICE",
                BranchId = warehouse.BranchId, Description = invoice.InvoiceNumber, Status = VoucherStatus.Posted
            };
            void Add(string account, decimal debit, decimal credit) => journal.Lines.Add(new JournalEntryLine
            {
                AccountId = accounts[account], DebitAmount = debit, CreditAmount = credit, PartnerId = customer?.Id, Description = invoice.InvoiceNumber
            });
            if (invoice.PaidAmount > 0) Add(cashAccount, invoice.PaidAmount, 0);
            if (invoice.RemainingDebt > 0) Add("131", invoice.RemainingDebt, 0);
            Add("511", 0, invoice.TotalAmount);
            if (issue.TotalCostAmount > 0) { Add("632", issue.TotalCostAmount, 0); Add("156", 0, issue.TotalCostAmount); }
            ValidateJournal(journal);
            invoice.JournalEntry = journal;
            context.SalesInvoices.Add(invoice);
            await context.SaveChangesAsync(cancellationToken);
            return Map(await LoadAsync(invoice.Id, cancellationToken));
        }, cancellationToken);
    }

    private Task<SalesInvoice> LoadAsync(int id, CancellationToken ct) => context.SalesInvoices
        .Include(i => i.Branch).Include(i => i.Items).ThenInclude(i => i.Item).SingleAsync(i => i.Id == id, ct);

    private static SalesInvoiceDto Map(SalesInvoice i) => new()
    {
        Id = i.Id, InvoiceNumber = i.InvoiceNumber, InvoiceDate = i.InvoiceDate, BranchId = i.BranchId,
        BranchCode = i.Branch?.Code, BranchName = i.Branch?.Name, CustomerName = i.CustomerName,
        PaymentMethodName = i.PaymentMethod switch { PaymentMethod.Cash => "sales.cash", PaymentMethod.BankTransfer => "sales.bank_transfer", _ => "sales.credit" },
        Status = i.Status.ToString(), SubTotal = i.SubTotal, DiscountAmount = i.DiscountAmount,
        TotalAmount = i.TotalAmount, PaidAmount = i.PaidAmount, TenderedAmount = i.TenderedAmount, ChangeAmount = i.ChangeAmount, RemainingDebt = i.RemainingDebt,
        Items = i.Items.Select(l => new SalesInvoiceItemDto
        {
            ItemId = l.ItemId, ItemCode = string.IsNullOrEmpty(l.ItemCode) ? l.Item?.Code ?? "" : l.ItemCode,
            ItemName = string.IsNullOrEmpty(l.ItemName) ? l.Item?.Name ?? "" : l.ItemName,
            Quantity = l.Quantity, UnitPrice = l.UnitPrice, Amount = l.Amount
        }).ToList()
    };
}
