using ERPm.Application.DTOs.Sales;
using ERPm.Application.Common.Interfaces;
using ERPm.Application.Features.Accounting;
using ERPm.Application.Features.Sales;
using ERPm.Application.Features.System;
using ERPm.Domain.Entities.Accounting;
using ERPm.Domain.Entities.Catalog;
using ERPm.Domain.Entities.Inventory;
using ERPm.Domain.Entities.Partner;
using ERPm.Domain.Entities.Sales;
using ERPm.Domain.Services.Sales;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Services.Accounting;
using ERPm.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;
using static ERPm.Infrastructure.Services.Common.PostingTransaction;

namespace ERPm.Infrastructure.Services;

public partial class SalesInvoiceService(ApplicationDbContext context, IBranchService branches, PostingTransaction posting, ICurrentUser? user = null, ERPm.Application.Common.Notifications.INotificationEngine? notificationEngine = null,
    ERPm.Application.Features.System.IIamService? iam = null, IDynamicPostingEngine? postingEngine = null) : ISalesInvoiceService
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

    /// <summary>Posted invoices of one customer with a receivable left, oldest first (the order a FIFO allocation uses).</summary>
    public async Task<List<OutstandingInvoiceDto>> GetOutstandingInvoicesAsync(int customerId, CancellationToken cancellationToken = default)
        => await context.SalesInvoices.AsNoTracking()
            .Where(i => i.CustomerId == customerId && i.Status == VoucherStatus.Posted && i.RemainingDebt > 0)
            .OrderBy(i => i.InvoiceDate).ThenBy(i => i.Id)
            .Select(i => new OutstandingInvoiceDto { Id = i.Id, InvoiceNumber = i.InvoiceNumber, InvoiceDate = i.InvoiceDate, BranchId = i.BranchId,
                TotalAmount = i.TotalAmount, PaidAmount = i.PaidAmount, RemainingDebt = i.RemainingDebt })
            .ToListAsync(cancellationToken);

    public async Task<SalesInvoiceDto?> GetInvoiceAsync(int id, CancellationToken cancellationToken = default)
    {
        var invoice = await context.SalesInvoices.AsNoTracking().Include(i => i.Branch).Include(i => i.Items).ThenInclude(i => i.Item)
            .SingleOrDefaultAsync(i => i.Id == id, cancellationToken);
        return invoice == null ? null : Map(invoice);
    }

    public Task<SalesInvoiceDto> CreateInvoiceAsync(CreateSalesInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        Require(request.RequestId != Guid.Empty && request.WarehouseId > 0 && request.Items is { Count: > 0 and <= 200 }, "error.validation.request");
        Require(Enum.IsDefined(request.PaymentMethod), "error.validation.payment_method");
        ValidateNumber(request.DiscountAmount);
        ValidateNumber(request.TaxRate);
        Require(request.TaxRate <= 100, "error.validation.request");
        ValidateNumber(request.TaxAmount);
        ValidateNumber(request.PaidAmount);
        foreach (var line in request.Items)
        {
            Require(line.ItemId > 0 && line.DiscountPercent is >= 0 and <= 100, "error.validation.request");
            ValidateNumber(line.Quantity, true);
            ValidateNumber(line.UnitPrice);
            ValidateNumber(line.DiscountPercent);
        }
        var hash = Hash(request);
        return PostAndNotifyAsync(() => posting.RunAsync(() => PostInvoiceAsync(request, hash, null, cancellationToken), cancellationToken), cancellationToken);
    }

    // Low-stock notices collected while posting; sent only after the commit, to accounts that may view stock in the warehouse's branch.
    private readonly List<DocumentNotice> lowStockNotices = [];

    private async Task<SalesInvoiceDto> PostAndNotifyAsync(Func<Task<SalesInvoiceDto>> post, CancellationToken ct)
    {
        lowStockNotices.Clear();
        var result = await post();
        foreach (var notice in lowStockNotices.ToList()) await PermissionRecipients.PublishAsync(context, iam, notificationEngine, user, notice, ct);
        lowStockNotices.Clear();
        return result;
    }

    /// <summary>Posts an invoice inside the caller's transaction. <paramref name="salesOrderId"/> marks an invoice raised from an order, whose agreed prices are kept.</summary>
    private async Task<SalesInvoiceDto> PostInvoiceAsync(CreateSalesInvoiceRequest request, string hash, int? salesOrderId, CancellationToken cancellationToken)
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
                Require(balances.TryGetValue(group.Key, out var balance) && balance.QuantityOnHand - balance.QuantityReserved >= group.Sum(i => i.Quantity), "error.stock.insufficient", 409);

            var invoice = new SalesInvoice
            {
                RequestId = request.RequestId, RequestHash = hash,
                InvoiceNumber = await posting.NumberAsync("HD", cancellationToken), BranchId = warehouse!.BranchId, DepartmentId = request.DepartmentId,
                WarehouseId = warehouse.Id, CustomerId = request.CustomerId, PaymentMethod = request.PaymentMethod,
                CashFundId = request.CashFundId, BankAccountId = request.BankAccountId,
                CustomerName = request.CustomerName.Trim(), DiscountAmount = request.DiscountAmount, Notes = request.Notes, Status = VoucherStatus.Posted, SalesOrderId = salesOrderId
            };
            var issue = new StockIssue
            {
                Code = await posting.NumberAsync("PXK", cancellationToken), BranchId = warehouse.BranchId, DepartmentId = request.DepartmentId,
                WarehouseId = warehouse.Id, CustomerId = request.CustomerId, Status = VoucherStatus.Posted, Reason = invoice.InvoiceNumber
            };
            foreach (var line in request.Items)
            {
                var item = items[line.ItemId];
                // An order carries the price agreed with the customer (price list, quotation), which may differ from the catalogue price.
                Require(salesOrderId != null || user == null || user.IsSystem || user.IsAdmin || line.UnitPrice == item.SalePrice, "error.sales.price_override", 403);
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

                    if (item.MinStockAlert > 0 && balances[item.Id].QuantityOnHand <= item.MinStockAlert)
                        lowStockNotices.Add(new DocumentNotice(
                            "WMS_LOW_STOCK", ERPm.Domain.Enums.NotificationCategory.WMS, ERPm.Domain.Enums.NotificationSeverity.WARNING, "wms:stock:view",
                            warehouse.BranchId, null, null,
                            new Dictionary<string, object>
                            {
                                ["item_name"] = item.Name, ["item_code"] = item.Code, ["warehouse_name"] = warehouse.Name,
                                ["current_stock"] = balances[item.Id].QuantityOnHand, ["min_stock"] = item.MinStockAlert, ["uom"] = item.BaseUom?.Name ?? "cái"
                            }, $"/inventory?search={item.Code}"));
                }
            }
            invoice.SubTotal = invoice.Items.Sum(i => i.Amount);
            Require(request.DiscountAmount <= invoice.SubTotal, "error.sales.discount");
            var netAmount = invoice.SubTotal - request.DiscountAmount;
            invoice.TaxRate = request.TaxRate;
            invoice.TaxAmount = request.TaxAmount > 0
                ? request.TaxAmount
                : (request.TaxRate > 0 ? Round(netAmount * request.TaxRate / 100m) : 0m);
            invoice.TotalAmount = netAmount + invoice.TaxAmount;
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
                var receivables = await posting.ReceivablesAsync(customer!.Id, cancellationToken);
                // Going over the limit was approved when the order was confirmed (who and when are kept on the order), so
                // invoicing that order on credit is not stopped again. Any other sale is held to the limit as before.
                var approvedOnOrder = salesOrderId.HasValue && await context.SalesOrders.IgnoreQueryFilters()
                    .AnyAsync(o => o.Id == salesOrderId && !o.IsDeleted && o.CreditCheckStatus == CreditCheckStatus.Overridden, cancellationToken);
                Require(approvedOnOrder || CreditLimitPolicy.Evaluate(customer!.IsUnlimitedCredit, customer.CreditLimit, receivables + invoice.RemainingDebt) != CreditLevel.Exceeded, "error.sales.credit_limit", 409);
                customer.DebtBalance += invoice.RemainingDebt;
            }
            issue.TotalCostAmount = issue.Items.Sum(i => i.TotalCost);
            ValidateNumber(issue.TotalCostAmount);
            if (issue.Items.Count > 0) invoice.StockIssue = issue;
            var engine = postingEngine ?? new DynamicPostingEngine(context);
            var dimensionKey = request.PaymentMethod switch
            {
                PaymentMethod.BankTransfer => "BANK",
                PaymentMethod.Cash => "CASH",
                _ => "DEBT"
            };
            var revenueRule = await engine.ResolvePostingRuleAsync("SD", "SALES_REVENUE", warehouse.BranchId, dimensionKey, cancellationToken);
            var journal = new JournalEntry
            {
                VoucherNumber = await posting.NumberAsync("PKT", cancellationToken), VoucherType = "SALES_INVOICE",
                BranchId = warehouse.BranchId, Description = invoice.InvoiceNumber, Status = VoucherStatus.Posted
            };
            void Add(int accountId, decimal debit, decimal credit) => journal.Lines.Add(new JournalEntryLine
            {
                AccountId = accountId, DebitAmount = debit, CreditAmount = credit, PartnerId = customer?.Id, Description = invoice.InvoiceNumber
            });
            if (invoice.PaidAmount > 0)
            {
                var paymentAccountId = request.PaymentMethod == PaymentMethod.BankTransfer
                    ? await engine.ResolveBankAccountIdAsync(warehouse.BranchId, request.BankAccountId, cancellationToken)
                    : await engine.ResolveCashAccountIdAsync(warehouse.BranchId, request.CashFundId, cancellationToken);
                Add(paymentAccountId, invoice.PaidAmount, 0);
            }
            if (invoice.RemainingDebt > 0)
            {
                var receivableAccountId = await engine.ResolveReceivableAccountIdAsync(warehouse.BranchId, customer?.Id, cancellationToken);
                Add(receivableAccountId, invoice.RemainingDebt, 0);
            }
            if (netAmount > 0) Add(revenueRule.CreditAccountId, 0, netAmount);
            if (invoice.TaxAmount > 0)
            {
                var vatRule = await engine.ResolvePostingRuleAsync("SD", "SALES_VAT", warehouse.BranchId, null, cancellationToken);
                Add(vatRule.CreditAccountId, 0, invoice.TaxAmount);
            }
            if (issue.TotalCostAmount > 0)
            {
                var cogsRule = await engine.ResolvePostingRuleAsync("SD", "SALES_COGS", warehouse.BranchId, null, cancellationToken);
                Add(cogsRule.DebitAccountId, issue.TotalCostAmount, 0);
                Add(cogsRule.CreditAccountId, 0, issue.TotalCostAmount);
            }
            await engine.ValidateJournalAsync(journal, cancellationToken);
            invoice.JournalEntry = journal;
            context.SalesInvoices.Add(invoice);
            await context.SaveChangesAsync(cancellationToken);
            return Map(await LoadAsync(invoice.Id, cancellationToken));
    }

    private Task<SalesInvoice> LoadAsync(int id, CancellationToken ct) => context.SalesInvoices
        .Include(i => i.Branch).Include(i => i.Items).ThenInclude(i => i.Item).SingleAsync(i => i.Id == id, ct);

    private static SalesInvoiceDto Map(SalesInvoice i) => new()
    {
        Id = i.Id, SalesOrderId = i.SalesOrderId, InvoiceNumber = i.InvoiceNumber, InvoiceDate = i.InvoiceDate, BranchId = i.BranchId, DepartmentId = i.DepartmentId,
        BranchCode = i.Branch?.Code, BranchName = i.Branch?.Name, CustomerName = i.CustomerName,
        PaymentMethodName = i.PaymentMethod switch { PaymentMethod.Cash => "sales.cash", PaymentMethod.BankTransfer => "sales.bank_transfer", _ => "sales.credit" },
        CashFundId = i.CashFundId, BankAccountId = i.BankAccountId,
        Status = i.Status.ToString(), SubTotal = i.SubTotal, DiscountAmount = i.DiscountAmount, TaxRate = i.TaxRate, TaxAmount = i.TaxAmount,
        TotalAmount = i.TotalAmount, PaidAmount = i.PaidAmount, TenderedAmount = i.TenderedAmount, ChangeAmount = i.ChangeAmount, RemainingDebt = i.RemainingDebt,
        Items = i.Items.Select(l => new SalesInvoiceItemDto
        {
            Id = l.Id, ItemId = l.ItemId, ItemCode = string.IsNullOrEmpty(l.ItemCode) ? l.Item?.Code ?? "" : l.ItemCode,
            ItemName = string.IsNullOrEmpty(l.ItemName) ? l.Item?.Name ?? "" : l.ItemName,
            Quantity = l.Quantity, UnitPrice = l.UnitPrice, Amount = l.Amount
        }).ToList()
    };
}
