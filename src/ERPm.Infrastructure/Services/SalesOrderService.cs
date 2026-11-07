using ERPm.Application.Common;
using ERPm.Application.Common.Crud;
using ERPm.Application.DTOs.Sales;
using ERPm.Domain.Entities.Catalog;
using ERPm.Domain.Entities.Sales;
using ERPm.Domain.Entities.Accounting;
using ERPm.Domain.Entities.Inventory;
using ERPm.Domain.Entities.Partner;
using ERPm.Domain.Services.Sales;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;
using static ERPm.Infrastructure.Services.Common.PostingTransaction;
namespace ERPm.Infrastructure.Services;
public sealed class SalesOrderService(ApplicationDbContext context, PostingTransaction posting, ERPm.Application.Common.Notifications.INotificationEngine? notificationEngine = null,
    ERPm.Application.Features.System.IIamService? iam = null, ERPm.Application.Common.Interfaces.ICurrentUser? currentUser = null)
    : BaseCrudService<SalesOrder, SalesOrderDto, CreateSalesOrderRequest, UpdateSalesOrderRequest>(context), IOrderWorkflow<SalesOrderDto>, IOrderApproval<SalesOrderDto>
{
    private OrderStockReservations Reservations => new(Context, posting);
    // An order whose total reaches this share of the credit limit is flagged with a warning.
    private static readonly string[] CommittedStatuses = [SalesOrderStatus.Confirmed, SalesOrderStatus.Processing];

    protected override IQueryable<SalesOrder> PrepareQuery(IQueryable<SalesOrder> query) => query.Include(o => o.Items).Include(o => o.Salesperson).Include(o => o.Quotation);
    protected override Task<SalesOrder?> FindEntityAsync(int id, CancellationToken ct) => Context.Set<SalesOrder>().Include(o => o.Items).Include(o => o.Salesperson).Include(o => o.Quotation).SingleOrDefaultAsync(o => o.Id == id, ct);
    protected override IQueryable<SalesOrder> ApplyFilters(IQueryable<SalesOrder> query, BaseFilterQuery filter)
    {
        query = base.ApplyFilters(query, filter);
        if (!string.IsNullOrWhiteSpace(filter.Status)) { var status = filter.Status.Trim(); query = query.Where(o => o.Status == status); }
        return filter.SalespersonId.HasValue ? query.Where(o => o.SalespersonId == filter.SalespersonId) : query;
    }
    protected override SalesOrderDto MapToDto(SalesOrder o) => new()
    {
        Id = o.Id, OrderNumber = o.OrderNumber, OrderDate = o.OrderDate, BranchId = o.BranchId, DepartmentId = o.DepartmentId,
        CustomerId = o.CustomerId, CustomerName = o.CustomerName,
        SubTotal = o.SubTotal, DiscountAmount = o.DiscountAmount, TaxAmount = o.TaxAmount, TotalAmount = o.TotalAmount, PaidAmount = o.PaidAmount,
        Status = o.Status, CreditCheckStatus = o.CreditCheckStatus, ReservationStatus = o.ReservationStatus, Notes = o.Notes,
        ExpectedDeliveryDate = o.ExpectedDeliveryDate, DeliveryAddress = o.DeliveryAddress, DeliveryAddressId = o.DeliveryAddressId,
        DeliveryRecipientName = o.DeliveryRecipientName, DeliveryPhone = o.DeliveryPhone, DeliveryAddressText = o.DeliveryAddressText, QuotationId = o.QuotationId, QuotationNumber = o.Quotation?.QuotationNumber,
        SalespersonId = o.SalespersonId, SalespersonName = o.Salesperson?.FullName, PriceListId = o.PriceListId,
        Items = o.Items.OrderBy(i => i.Id).Select(i => new SalesOrderItemDto
        {
            Id = i.Id, ItemId = i.ItemId, ItemCode = i.ItemCode, ItemName = i.ItemName, UnitName = i.UnitName, Quantity = i.Quantity,
            UnitPrice = i.UnitPrice, DiscountAmount = i.DiscountAmount, LineTotal = i.LineTotal, VatRate = i.VatRate, TaxAmount = i.TaxAmount, InvoicedQuantity = i.InvoicedQuantity
        }).ToList()
    };
    // The branch name is looked up apart: see BranchNames.
    public override async Task<PagedResult<SalesOrderDto>> GetPagedAsync(BaseFilterQuery query, CancellationToken ct = default)
    {
        var page = await base.GetPagedAsync(query, ct);
        await NameBranchesAsync(page.Items, ct);
        return page;
    }
    public override async Task<SalesOrderDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var dto = await base.GetByIdAsync(id, ct);
        return dto == null ? null : await NamedAsync(dto, ct);
    }
    private async Task<SalesOrderDto> NamedAsync(SalesOrderDto dto, CancellationToken ct) { await NameBranchesAsync([dto], ct); return dto; }
    private async Task NameBranchesAsync(IEnumerable<SalesOrderDto> dtos, CancellationToken ct)
    {
        var names = await BranchNames.ForAsync(Context, dtos.Select(d => d.BranchId), ct);
        foreach (var dto in dtos) dto.BranchName = names.GetValueOrDefault(dto.BranchId);
    }
    protected override SalesOrder MapToEntity(CreateSalesOrderRequest request) => new();
    protected override async Task BeforeCreateAsync(SalesOrder entity, CreateSalesOrderRequest request, CancellationToken ct)
    {
        await PrepareAsync(entity, request, ct);
        entity.OrderNumber = string.IsNullOrWhiteSpace(request.OrderNumber) ? await posting.NumberAsync("SO", ct) : request.OrderNumber.Trim();
        entity.Status = SalesOrderStatus.Draft;
    }
    protected override async Task BeforeUpdateAsync(SalesOrder entity, UpdateSalesOrderRequest request, CancellationToken ct)
    {
        Require(entity.Status == SalesOrderStatus.Draft && (request.Status == null || request.Status == SalesOrderStatus.Draft), "error.sd.order_invalid_status_for_action", 409);
        Context.RemoveRange(entity.Items);
        await PrepareAsync(entity, request, ct);
    }
    protected override void MapUpdateToEntity(UpdateSalesOrderRequest request, SalesOrder entity) { }
    protected override Task BeforeDeleteAsync(SalesOrder entity, CancellationToken ct)
    {
        Require(entity.Status == SalesOrderStatus.Draft, "error.sd.order_invalid_status_for_action", 409);
        return Task.CompletedTask;
    }
    // The saved entity has no navigation data yet, so return the freshly loaded order.
    public override async Task<SalesOrderDto> CreateAsync(CreateSalesOrderRequest request, CancellationToken ct = default)
    {
        var created = await base.CreateAsync(request, ct);
        Context.ChangeTracker.Clear();
        return (await GetByIdAsync(created.Id, ct))!;
    }
    public override Task<SalesOrderDto?> UpdateAsync(int id, UpdateSalesOrderRequest request, CancellationToken ct = default) => posting.RunAsync(async () =>
    {
        await posting.LockAsync($"order:Sales:{id}", ct);
        var updated = await base.UpdateAsync(id, request, ct);
        if (updated == null) return null;
        Context.ChangeTracker.Clear();
        return await GetByIdAsync(id, ct);
    }, ct);
    public override Task<bool> DeleteAsync(int id, CancellationToken ct = default) => posting.RunAsync(async () =>
    {
        await posting.LockAsync($"order:Sales:{id}", ct);
        return await base.DeleteAsync(id, ct);
    }, ct);

    /// <summary>
    /// Confirms a Draft order. The customer's credit is checked first: an order over the limit waits for
    /// <c>sd:order:approve_credit</c> and holds no stock; otherwise it is confirmed and its stock reserved.
    /// </summary>
    public async Task<SalesOrderDto> ConfirmAsync(int id, CancellationToken ct)
    {
        DocumentNotice? publish = null;
        var dto = await posting.RunAsync(async () =>
        {
            await posting.LockAsync($"order:Sales:{id}", ct);
            var entity = await FindEntityAsync(id, ct);
            Require(entity != null, "error.order.not_found", 404);
            Require(entity!.Status == SalesOrderStatus.Draft, "error.sd.order_invalid_status_for_action", 409);

            entity.CreditCheckStatus = await CheckCreditAsync(entity, ct);
            if (entity.CreditCheckStatus == CreditCheckStatus.PendingApproval)
            {
                entity.Status = SalesOrderStatus.PendingApproval;
                publish = OrderEvent("SD_ORDER_CREDIT_PENDING", ERPm.Domain.Enums.NotificationSeverity.WARNING, entity, null);
            }
            else
            {
                await Reservations.ReserveAsync(entity, ct);
                entity.Status = SalesOrderStatus.Confirmed;
                publish = OrderEvent("SD_ORDER_NEW", ERPm.Domain.Enums.NotificationSeverity.INFO, entity, null);
            }
            await Context.SaveChangesAsync(ct);
            return await NamedAsync(MapToDto(entity), ct);
        }, ct);
        await PublishAsync(publish, ct);
        return dto;
    }

    public async Task<SalesOrderDto> ApproveCreditAsync(int id, string reason, CancellationToken ct)
    {
        Require(!string.IsNullOrWhiteSpace(reason) && reason.Trim().Length <= 500, "error.validation.request");
        DocumentNotice? publish = null;
        var dto = await posting.RunAsync(async () =>
        {
            await posting.LockAsync($"order:Sales:{id}", ct);
            var entity = await FindEntityAsync(id, ct);
            Require(entity != null, "error.order.not_found", 404);
            Require(entity!.Status == SalesOrderStatus.PendingApproval, "error.sd.order_invalid_status_for_action", 409);

            await Reservations.ReserveAsync(entity, ct);
            entity.CreditCheckStatus = CreditCheckStatus.Overridden;
            entity.CreditApprovalReason = reason.Trim();
            entity.Status = SalesOrderStatus.Confirmed;
            publish = OrderEvent("SD_ORDER_CREDIT_APPROVED", ERPm.Domain.Enums.NotificationSeverity.INFO, entity, reason.Trim());
            await Context.SaveChangesAsync(ct);
            return await NamedAsync(MapToDto(entity), ct);
        }, ct);
        await PublishAsync(publish, ct);
        return dto;
    }

    public async Task<SalesOrderDto> CancelAsync(int id, string reason, CancellationToken ct)
    {
        Require(!string.IsNullOrWhiteSpace(reason) && reason.Trim().Length <= 500, "error.validation.request");
        DocumentNotice? publish = null;
        var dto = await posting.RunAsync(async () =>
        {
            await posting.LockAsync($"order:Sales:{id}", ct);
            var entity = await FindEntityAsync(id, ct);
            Require(entity != null, "error.order.not_found", 404);
            Require(entity!.Items.All(i => i.InvoicedQuantity == 0), "error.sd.cannot_cancel_invoiced_order", 409);
            Require(entity.Status is SalesOrderStatus.Draft or SalesOrderStatus.PendingApproval or SalesOrderStatus.Confirmed, "error.sd.order_invalid_status_for_action", 409);

            await Reservations.ReleaseAsync(entity, ct);
            entity.Status = SalesOrderStatus.Cancelled;
            entity.CancelReason = reason.Trim();
            publish = OrderEvent("SD_ORDER_CANCELLED", ERPm.Domain.Enums.NotificationSeverity.WARNING, entity, reason.Trim());
            await Context.SaveChangesAsync(ct);
            return await NamedAsync(MapToDto(entity), ct);
        }, ct);
        await PublishAsync(publish, ct);
        return dto;
    }

    /// <summary>
    /// Credit exposure is posted receivables plus the value of other confirmed orders not invoiced yet plus this order.
    /// A customer-less (retail) order is not credit checked. Like invoice posting, a limit of 0 allows no credit.
    /// </summary>
    private async Task<string> CheckCreditAsync(SalesOrder order, CancellationToken ct)
    {
        if (!order.CustomerId.HasValue) return CreditCheckStatus.Passed;
        await posting.LockAsync($"partner:{order.CustomerId}", ct);
        var credit = await Context.Partners.Where(p => p.Id == order.CustomerId).Select(p => new { p.CreditLimit, p.IsUnlimitedCredit }).SingleAsync(ct);
        var receivables = await posting.ReceivablesAsync(order.CustomerId.Value, ct);
        var committed = await Context.SalesOrders.IgnoreQueryFilters()
            .Where(o => !o.IsDeleted && o.CustomerId == order.CustomerId && o.Id != order.Id && CommittedStatuses.Contains(o.Status)).SumAsync(o => o.TotalAmount, ct);
        var exposure = receivables + committed + order.TotalAmount;
        return CreditLimitPolicy.Evaluate(credit.IsUnlimitedCredit, credit.CreditLimit, exposure) switch
        {
            CreditLevel.Exceeded => CreditCheckStatus.PendingApproval,
            CreditLevel.Warning => CreditCheckStatus.Warning,
            _ => CreditCheckStatus.Passed
        };
    }

    private static DocumentNotice OrderEvent(string template, string severity, SalesOrder order, string? reason) => new(
        template, ERPm.Domain.Enums.NotificationCategory.SD, severity, "sd:order:view", order.BranchId, order.DepartmentId, order.CreatedBy,
        new Dictionary<string, object>
        {
            ["order_code"] = order.OrderNumber,
            ["customer_name"] = order.CustomerName,
            ["total_amount"] = order.TotalAmount,
            ["reason"] = reason ?? string.Empty
        }, "/sales/pos");

    // Sent after the transaction committed; only accounts that may view orders in a scope reaching this one are told,
    // never the person who made the change.
    private Task PublishAsync(DocumentNotice? notice, CancellationToken ct) => PermissionRecipients.PublishAsync(Context, iam, notificationEngine, currentUser, notice, ct);

    /// <summary>
    /// Takes a copy of the chosen address of the customer. Without a choice the copy is cleared, so an order never shows
    /// recipient data that no longer matches what was picked.
    /// </summary>
    private async Task ApplyDeliveryAddressAsync(SalesOrder entity, int? addressId, Partner? partner, int? storedAddressId, int? storedCustomerId, CancellationToken ct)
    {
        // Saving an order again with the address it already holds keeps its copy as it is, even when the source address
        // was changed or deleted since; only a different choice, or a different customer, is checked against the list.
        if (addressId.HasValue && addressId == storedAddressId && partner?.Id == storedCustomerId && entity.DeliveryAddressText != null)
        {
            entity.DeliveryAddress = entity.DeliveryAddressText;
            return;
        }
        entity.DeliveryAddressId = null; entity.DeliveryRecipientName = null; entity.DeliveryPhone = null; entity.DeliveryAddressText = null;
        if (!addressId.HasValue) return;
        var address = partner == null ? null : await Context.PartnerDeliveryAddresses.AsNoTracking().SingleOrDefaultAsync(a => a.Id == addressId && a.PartnerId == partner.Id, ct);
        Require(address != null, "error.sd.order_delivery_address_invalid");
        entity.DeliveryAddressId = address!.Id;
        entity.DeliveryRecipientName = address.RecipientName;
        entity.DeliveryPhone = address.Phone;
        entity.DeliveryAddressText = string.Join(", ", new[] { address.Address, address.Ward, address.District, address.City }.Where(p => !string.IsNullOrWhiteSpace(p)));
        entity.DeliveryAddress = entity.DeliveryAddressText;
    }

    private async Task PrepareAsync(SalesOrder entity, CreateSalesOrderRequest request, CancellationToken ct)
    {
        var storedAddressId = entity.DeliveryAddressId; var storedCustomerId = entity.CustomerId;
        // A seller limited to their own records has no branch in their data scope, so the branch is checked outside it.
        Require(request.Items is { Count: > 0 and <= 200 } && await Context.Branches.IgnoreQueryFilters().AnyAsync(b => !b.IsDeleted && b.Id == request.BranchId && b.IsActive, ct), "error.validation.request");
        Require(request.PaidAmount == 0, "error.validation.request");
        ValidateNumber(request.DiscountAmount); ValidateNumber(request.TaxAmount);
        Require(request.Items.All(l => l.VatRate >= 0 && l.VatRate <= 100 && decimal.Round(l.VatRate, 2) == l.VatRate), "error.sd.order_vat_rate_invalid");
        Require(request.SalespersonId == null || await Context.Employees.AnyAsync(e => e.Id == request.SalespersonId && e.IsActive, ct), "error.validation.reference");
        Require(request.QuotationId == null || await Context.SalesQuotations.AnyAsync(q => q.Id == request.QuotationId, ct), "error.validation.reference");
        Require(request.ExpectedDeliveryDate == null || request.ExpectedDeliveryDate.Value.Kind == DateTimeKind.Utc, "error.validation.request");
        Require(request.DeliveryAddress == null || request.DeliveryAddress.Length <= 500, "error.validation.request");
        var partner = request.CustomerId.HasValue ? await Context.Partners.SingleOrDefaultAsync(p => p.Id == request.CustomerId && p.IsActive, ct) : null;
        Require(!request.CustomerId.HasValue || partner != null && partner.PartnerType.HasFlag(PartnerType.Customer), "error.sales.customer");
        var ids = request.Items.Select(i => i.ItemId).Distinct().ToArray();
        var items = await Context.Items.Include(i => i.BaseUom).Where(i => ids.Contains(i.Id) && i.IsActive).ToDictionaryAsync(i => i.Id, ct);
        Require(items.Count == ids.Length, "error.inventory.item");
        entity.Items = request.Items.Select(line =>
        {
            ValidateNumber(line.Quantity, true); ValidateNumber(line.UnitPrice);
            var discount = line.DiscountAmount; ValidateNumber(discount);
            var amount = Round(line.Quantity * line.UnitPrice) - discount;
            Require(amount >= 0, "error.sales.discount");
            var item = items[line.ItemId];
            // VAT is worked out per line on the amount after the line discount and rounded to 4 decimals.
            return new SalesOrderItem { ItemId = item.Id, ItemCode = item.Code, ItemName = item.Name, UnitName = item.BaseUom?.Name, Quantity = line.Quantity, UnitPrice = line.UnitPrice, DiscountAmount = discount, LineTotal = amount,
                VatRate = line.VatRate, TaxAmount = Round(amount * line.VatRate / 100m) };
        }).ToList();
        entity.BranchId = request.BranchId; entity.DepartmentId = request.DepartmentId; entity.CustomerId = partner?.Id; entity.CustomerName = partner?.Name ?? request.CustomerName.Trim();
        entity.ExpectedDeliveryDate = request.ExpectedDeliveryDate; entity.QuotationId = request.QuotationId; entity.SalespersonId = request.SalespersonId;
        entity.DeliveryAddress = string.IsNullOrWhiteSpace(request.DeliveryAddress) ? partner?.Address : request.DeliveryAddress.Trim();
        await ApplyDeliveryAddressAsync(entity, request.DeliveryAddressId, partner, storedAddressId, storedCustomerId, ct);
        entity.SubTotal = entity.Items.Sum(i => i.LineTotal);
        Require(request.DiscountAmount <= entity.SubTotal, "error.sales.discount");
        entity.DiscountAmount = request.DiscountAmount;
        // The header VAT is the sum of the line VAT; a client may echo it but cannot set a different figure.
        var lineTax = entity.Items.Sum(i => i.TaxAmount);
        Require(request.TaxAmount == 0 || request.TaxAmount == lineTax, "error.validation.request");
        entity.TaxAmount = lineTax; entity.TotalAmount = entity.SubTotal - request.DiscountAmount + lineTax;
        ValidateNumber(entity.TotalAmount, true); entity.Notes = request.Notes;
        if (request.OrderDate.HasValue) { Require(request.OrderDate.Value.Kind == DateTimeKind.Utc, "error.validation.request"); entity.OrderDate = request.OrderDate.Value; }
    }
}
