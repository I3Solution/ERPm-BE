using ERPm.Application.Common;
using ERPm.Application.Common.Crud;
using ERPm.Application.DTOs.Sales;
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

/// <summary>
/// Sales returns. Posting one puts the goods back into stock at the cost they left it, reverses revenue
/// (Dr 521) and cost of sales (Dr 156 / Cr 632), and refunds the customer in cash, by bank or against receivables.
/// Posted returns cannot be edited or deleted.
/// </summary>
public sealed class SalesReturnService(ApplicationDbContext context, PostingTransaction posting, ERPm.Application.Common.Notifications.INotificationEngine? notificationEngine = null,
    ERPm.Application.Features.System.IIamService? iam = null, ERPm.Application.Common.Interfaces.ICurrentUser? currentUser = null)
    : BaseCrudService<SalesReturn, SalesReturnDetailDto, CreateSalesReturnRequest, CreateSalesReturnRequest>(context)
{
    protected override IQueryable<SalesReturn> PrepareQuery(IQueryable<SalesReturn> query) => query.Include(r => r.Invoice);

    protected override Task<SalesReturn?> FindEntityAsync(int id, CancellationToken ct) => Context.SalesReturns
        .Include(r => r.Invoice).Include(r => r.Items)
        .SingleOrDefaultAsync(r => r.Id == id, ct);

    protected override IQueryable<SalesReturn> ApplyFilters(IQueryable<SalesReturn> query, BaseFilterQuery filter)
    {
        query = base.ApplyFilters(query, filter);
        if (!string.IsNullOrWhiteSpace(filter.Status) && Enum.TryParse<VoucherStatus>(filter.Status.Trim(), true, out var status)) query = query.Where(r => r.Status == status);
        return query;
    }

    protected override SalesReturnDetailDto MapToDto(SalesReturn r) => new()
    {
        Id = r.Id, ExternalId = r.ExternalId, ReturnNumber = r.ReturnNumber, ReturnDate = r.ReturnDate,
        InvoiceId = r.InvoiceId, InvoiceNumber = r.Invoice?.InvoiceNumber, CustomerId = r.CustomerId, CustomerName = r.CustomerName,
        BranchId = r.BranchId, WarehouseId = r.WarehouseId, TotalRefundAmount = r.TotalRefundAmount, RefundMethod = (int)r.RefundMethod,
        Status = r.Status.ToString(), StockReceiptId = r.StockReceiptId, JournalEntryId = r.JournalEntryId, Reason = r.Reason, Notes = r.Notes,
        Items = r.Items.OrderBy(i => i.Id).Select(i => new SalesReturnItemDto
        {
            Id = i.Id, InvoiceItemId = i.InvoiceItemId, ItemId = i.ItemId, ItemCode = i.ItemCode, ItemName = i.ItemName, UnitName = i.UnitName,
            ReturnQuantity = i.ReturnQuantity, UnitPrice = i.UnitPrice, RefundAmount = i.RefundAmount, Reason = i.Reason
        }).ToList()
    };

    // A posted return is final: corrections are made with a new document, never by editing.
    public override Task<SalesReturnDetailDto?> UpdateAsync(int id, CreateSalesReturnRequest request, CancellationToken ct = default) =>
        throw new BusinessException("error.accounting.posted_immutable", 409);

    public override Task<bool> DeleteAsync(int id, CancellationToken ct = default) =>
        throw new BusinessException("error.accounting.posted_immutable", 409);

    public override async Task<SalesReturnDetailDto> CreateAsync(CreateSalesReturnRequest request, CancellationToken ct = default)
    {
        Require(Enum.IsDefined((RefundMethod)request.RefundMethod) && request.BranchId > 0 && request.WarehouseId > 0, "error.validation.request");
        Require(!string.IsNullOrWhiteSpace(request.Reason) && request.Reason.Trim().Length <= 500, "error.validation.request");
        Require(request.Items is { Count: > 0 and <= 200 }, "error.validation.request");
        foreach (var line in request.Items)
        {
            Require(line.ItemId > 0 && (line.Reason == null || line.Reason.Length <= 500), "error.validation.request");
            ValidateNumber(line.Quantity, true);
            if (request.InvoiceId == null) ValidateNumber(line.UnitPrice);
        }
        var method = (RefundMethod)request.RefundMethod;

        DocumentNotice? publish = null;
        var id = await posting.RunAsync(async () =>
        {
            var warehouse = await Context.Warehouses.SingleOrDefaultAsync(w => w.Id == request.WarehouseId && w.IsActive && w.Branch!.IsActive, ct);
            Require(warehouse != null, "error.inventory.warehouse", 404);
            Require(warehouse!.BranchId == request.BranchId, "error.inventory.branch_mismatch");

            SalesInvoice? invoice = null;
            if (request.InvoiceId.HasValue)
            {
                await posting.LockAsync($"sale-cancel:{request.InvoiceId}", ct);       // shared with invoice cancellation
                await posting.LockAsync($"sale-return:{request.InvoiceId}", ct);       // serializes returns of one invoice
                invoice = await Context.SalesInvoices.Include(i => i.Items).SingleOrDefaultAsync(i => i.Id == request.InvoiceId, ct);
                Require(invoice != null, "error.validation.reference", 404);
                Require(invoice!.Status == VoucherStatus.Posted, "error.sales.cancelled", 409);
                Require(invoice.BranchId == request.BranchId && (request.CustomerId == null || request.CustomerId == invoice.CustomerId), "error.validation.request");
            }

            var customerId = invoice != null ? invoice.CustomerId : request.CustomerId;
            Require(customerId.HasValue || method != RefundMethod.ReduceDebt, "error.sales.customer_required");
            Partner? customer = null;
            if (customerId.HasValue)
            {
                await posting.LockAsync($"partner:{customerId}", ct);
                customer = await Context.Partners.SingleOrDefaultAsync(p => p.Id == customerId && p.IsActive, ct);
                Require(customer != null && customer.PartnerType.HasFlag(PartnerType.Customer), "error.sales.customer");
            }

            var itemIds = request.Items.Select(i => i.ItemId).Distinct().Order().ToArray();
            var items = await Context.Items.Include(i => i.BaseUom).Where(i => itemIds.Contains(i.Id) && i.IsActive).ToDictionaryAsync(i => i.Id, ct);
            Require(items.Count == itemIds.Length && items.Values.All(i => i.ItemType != ItemType.Combo), "error.inventory.item");
            var goodsIds = itemIds.Where(i => items[i].ItemType == ItemType.Goods).ToArray();
            foreach (var itemId in goodsIds) await posting.LockAsync($"stock:{warehouse.Id}:{itemId}", ct);
            var balances = await Context.StockBalances.Where(b => b.WarehouseId == warehouse.Id && goodsIds.Contains(b.ItemId)).ToDictionaryAsync(b => b.ItemId, ct);

            var lines = invoice != null ? await InvoiceLinesAsync(invoice, request, items, ct) : FreeLines(request, items, balances);

            var refund = lines.Sum(l => l.Line.RefundAmount);
            var cost = lines.Sum(l => l.Line.CostAmount);
            ValidateNumber(refund, true); ValidateNumber(cost);

            if (method == RefundMethod.ReduceDebt)
            {
                var receivables = await posting.ReceivablesAsync(customer!.Id, ct);
                Require(refund <= receivables, "error.sd.return_refund_exceeds_debt", 409);
                customer.DebtBalance -= refund;
            }

            // Goods go back into the warehouse at the cost they left it.
            var receipt = new StockReceipt
            {
                Code = await posting.NumberAsync("PNK", ct), BranchId = warehouse.BranchId, DepartmentId = invoice?.DepartmentId, WarehouseId = warehouse.Id,
                Status = VoucherStatus.Posted
            };
            foreach (var (line, item, _) in lines.Where(l => l.Goods))
            {
                if (!balances.TryGetValue(item.Id, out var balance))
                {
                    balance = new StockBalance { WarehouseId = warehouse.Id, ItemId = item.Id };
                    Context.StockBalances.Add(balance);
                    balances[item.Id] = balance;
                }
                balance.InventoryValue += line.CostAmount;
                balance.QuantityOnHand += line.ReturnQuantity;
                balance.AverageCostPrice = Round(balance.InventoryValue / balance.QuantityOnHand);
                receipt.Items.Add(new StockReceiptItem { ItemId = item.Id, Quantity = line.ReturnQuantity, UnitPrice = Round(line.CostAmount / line.ReturnQuantity), Amount = line.CostAmount });
            }
            receipt.TotalAmount = receipt.Items.Sum(i => i.Amount);

            var refundAccount = method switch { RefundMethod.BankTransfer => "112", RefundMethod.ReduceDebt => "131", _ => "111" };
            var codes = new List<string> { "521", refundAccount };
            if (cost > 0) codes.AddRange(["632", "156"]);
            var accounts = await posting.AccountsAsync(codes, ct);

            var number = $"SR-{DateTime.UtcNow:yyyyMM}-{await posting.NextValueAsync(ct):D4}";
            var journal = new JournalEntry
            {
                VoucherNumber = await posting.NumberAsync("PKT", ct), VoucherType = "SALES_RETURN", BranchId = warehouse.BranchId,
                Description = number, Status = VoucherStatus.Posted
            };
            void Add(string account, decimal debit, decimal credit) => journal.Lines.Add(new JournalEntryLine
            {
                AccountId = accounts[account], DebitAmount = debit, CreditAmount = credit, PartnerId = customer?.Id, Description = number
            });
            Add("521", refund, 0);
            Add(refundAccount, 0, refund);
            if (cost > 0) { Add("156", cost, 0); Add("632", 0, cost); }
            ValidateJournal(journal);

            var salesReturn = new SalesReturn
            {
                ReturnNumber = number, InvoiceId = invoice?.Id, CustomerId = customer?.Id, CustomerName = customer?.Name ?? invoice?.CustomerName ?? "Khách lẻ",
                BranchId = warehouse.BranchId, DepartmentId = invoice?.DepartmentId, WarehouseId = warehouse.Id, RefundMethod = method, Status = VoucherStatus.Posted,
                TotalRefundAmount = refund, TotalCostAmount = cost, Reason = request.Reason.Trim(), Notes = request.Notes,
                JournalEntry = journal, Items = lines.Select(l => l.Line).ToList()
            };
            if (receipt.Items.Count > 0) { receipt.Notes = number; salesReturn.StockReceipt = receipt; }
            Context.SalesReturns.Add(salesReturn);
            await Context.SaveChangesAsync(ct);

            publish = new DocumentNotice(
                "SD_RETURN_POSTED", ERPm.Domain.Enums.NotificationCategory.SD, ERPm.Domain.Enums.NotificationSeverity.INFO, "sd:return:view",
                salesReturn.BranchId, salesReturn.DepartmentId, salesReturn.CreatedBy,
                new Dictionary<string, object> { ["return_code"] = number, ["customer_name"] = salesReturn.CustomerName, ["total_refund"] = refund },
                "/sales/returns");
            return salesReturn.Id;
        }, ct);

        // Sent after the commit, to accounts that may view returns in a scope reaching this one, never the person who posted it.
        await PermissionRecipients.PublishAsync(Context, iam, notificationEngine, currentUser, publish, ct);
        Context.ChangeTracker.Clear();
        return (await GetByIdAsync(id, ct))!;
    }

    /// <summary>
    /// Lines of a return against an invoice. The refund price and cost come from the invoice line (header discount
    /// shared proportionally), and the quantity cannot exceed what was invoiced minus what was already returned.
    /// </summary>
    private async Task<List<(SalesReturnItem Line, Item Item, bool Goods)>> InvoiceLinesAsync(SalesInvoice invoice, CreateSalesReturnRequest request, Dictionary<int, Item> items, CancellationToken ct)
    {
        Require(request.Items.All(l => l.InvoiceItemId.HasValue), "error.validation.request");
        var invoiceItems = invoice.Items.ToDictionary(i => i.Id);
        Require(request.Items.All(l => invoiceItems.TryGetValue(l.InvoiceItemId!.Value, out var ii) && ii.ItemId == l.ItemId), "error.validation.request");

        var ids = request.Items.Select(l => l.InvoiceItemId!.Value).Distinct().ToArray();
        var returned = await Context.SalesReturnItems.IgnoreQueryFilters()
            .Where(r => r.InvoiceItemId != null && ids.Contains(r.InvoiceItemId.Value) && !r.SalesReturn!.IsDeleted && r.SalesReturn.Status == VoucherStatus.Posted)
            .GroupBy(r => r.InvoiceItemId!.Value).Select(g => new { Id = g.Key, Quantity = g.Sum(r => r.ReturnQuantity) }).ToDictionaryAsync(g => g.Id, g => g.Quantity, ct);
        foreach (var group in request.Items.GroupBy(l => l.InvoiceItemId!.Value))
            Require(returned.GetValueOrDefault(group.Key) + group.Sum(l => l.Quantity) <= invoiceItems[group.Key].Quantity, "error.sd.return_quantity_exceeds_invoiced", 409);

        var share = invoice.SubTotal == 0 ? 1m : invoice.TotalAmount / invoice.SubTotal;
        return request.Items.Select(l =>
        {
            var source = invoiceItems[l.InvoiceItemId!.Value];
            var item = items[l.ItemId];
            var goods = item.ItemType == ItemType.Goods;
            var refund = Round(source.Amount * share * l.Quantity / source.Quantity);
            var line = new SalesReturnItem
            {
                InvoiceItemId = source.Id, ItemId = item.Id, ItemCode = item.Code, ItemName = item.Name, UnitName = item.BaseUom?.Name,
                ReturnQuantity = l.Quantity, UnitPrice = Round(refund / l.Quantity), RefundAmount = refund,
                CostAmount = goods ? Round(source.CostAmount * l.Quantity / source.Quantity) : 0m, Reason = l.Reason
            };
            return (line, item, goods);
        }).ToList();
    }

    /// <summary>Lines of a return without invoice: priced as requested and costed at the warehouse average cost.</summary>
    private static List<(SalesReturnItem Line, Item Item, bool Goods)> FreeLines(CreateSalesReturnRequest request, Dictionary<int, Item> items, Dictionary<int, StockBalance> balances) =>
        request.Items.Select(l =>
        {
            Require(!l.InvoiceItemId.HasValue, "error.validation.request");
            var item = items[l.ItemId];
            var goods = item.ItemType == ItemType.Goods;
            var unitCost = goods ? (balances.TryGetValue(item.Id, out var balance) && balance.QuantityOnHand > 0 ? balance.AverageCostPrice : item.CostPrice) : 0m;
            var line = new SalesReturnItem
            {
                ItemId = item.Id, ItemCode = item.Code, ItemName = item.Name, UnitName = item.BaseUom?.Name,
                ReturnQuantity = l.Quantity, UnitPrice = l.UnitPrice, RefundAmount = Round(l.Quantity * l.UnitPrice),
                CostAmount = Round(l.Quantity * unitCost), Reason = l.Reason
            };
            return (line, item, goods);
        }).ToList();
}
