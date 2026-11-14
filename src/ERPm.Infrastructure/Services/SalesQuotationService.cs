using ERPm.Application.Common;
using ERPm.Application.Common.Crud;
using ERPm.Application.DTOs.Sales;
using ERPm.Domain.Entities.Partner;
using ERPm.Domain.Entities.Sales;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;
using static ERPm.Infrastructure.Services.Common.PostingTransaction;

namespace ERPm.Infrastructure.Services;

/// <summary>Sales quotation CRUD, status changes and the one-click conversion into a Draft sales order.</summary>
public sealed class SalesQuotationService(ApplicationDbContext context, PostingTransaction posting, ERPm.Application.Common.Notifications.INotificationEngine? notificationEngine = null,
    ERPm.Application.Features.System.IIamService? iam = null, ERPm.Application.Common.Interfaces.ICurrentUser? currentUser = null)
    : BaseCrudService<SalesQuotation, SalesQuotationDetailDto, CreateSalesQuotationRequest, UpdateSalesQuotationRequest>(context), IQuotationWorkflow
{
    private const int MaxLines = 200;
    private static readonly string[] OpenStatuses = [QuotationStatus.Draft, QuotationStatus.Sent, QuotationStatus.Accepted];

    protected override IQueryable<SalesQuotation> PrepareQuery(IQueryable<SalesQuotation> query) => query.Include(q => q.Salesperson);

    protected override Task<SalesQuotation?> FindEntityAsync(int id, CancellationToken ct) => Context.SalesQuotations
        .Include(q => q.Salesperson)
        .Include(q => q.Items)
        .SingleOrDefaultAsync(q => q.Id == id, ct);

    protected override IQueryable<SalesQuotation> ApplyFilters(IQueryable<SalesQuotation> query, BaseFilterQuery filter)
    {
        query = base.ApplyFilters(query, filter);
        if (filter.SalespersonId.HasValue) query = query.Where(q => q.SalespersonId == filter.SalespersonId);
        if (string.IsNullOrWhiteSpace(filter.Status)) return query;

        var now = DateTime.UtcNow;
        var status = filter.Status.Trim();
        // "Expired" is derived: a not yet closed quotation whose validity date has passed.
        if (status == QuotationStatus.Expired) return query.Where(q => OpenStatuses.Contains(q.Status) && q.ValidUntil < now);
        return OpenStatuses.Contains(status)
            ? query.Where(q => q.Status == status && q.ValidUntil >= now)
            : query.Where(q => q.Status == status);
    }

    protected override SalesQuotationDetailDto MapToDto(SalesQuotation entity) => new()
    {
        Id = entity.Id,
        ExternalId = entity.ExternalId,
        QuotationNumber = entity.QuotationNumber,
        QuotationDate = entity.QuotationDate,
        ValidUntil = entity.ValidUntil,
        CustomerId = entity.CustomerId,
        CustomerName = entity.CustomerName,
        CustomerPhone = entity.CustomerPhone,
        CustomerEmail = entity.CustomerEmail,
        SalespersonId = entity.SalespersonId,
        SalespersonName = entity.Salesperson?.FullName,
        BranchId = entity.BranchId,
        DepartmentId = entity.DepartmentId,
        PriceListId = entity.PriceListId,
        DeliveryAddressId = entity.DeliveryAddressId,
        DeliveryRecipientName = entity.DeliveryRecipientName,
        DeliveryPhone = entity.DeliveryPhone,
        DeliveryAddressText = entity.DeliveryAddressText,
        Status = EffectiveStatus(entity),
        SubTotal = entity.SubTotal,
        DiscountAmount = entity.DiscountAmount,
        TaxAmount = entity.TaxAmount,
        TotalAmount = entity.TotalAmount,
        TermsAndConditions = entity.TermsAndConditions,
        ConvertedSalesOrderId = entity.ConvertedSalesOrderId,
        Items = entity.Items.OrderBy(i => i.Id).Select(i => new SalesQuotationItemDto
        {
            Id = i.Id,
            ItemId = i.ItemId,
            ItemCode = i.ItemCode,
            ItemName = i.ItemName,
            UnitName = i.UnitName,
            Quantity = i.Quantity,
            UnitPrice = i.UnitPrice,
            DiscountAmount = i.DiscountAmount,
            LineTotal = i.LineTotal,
            Notes = i.Notes
        }).ToList()
    };

    // The branch name is looked up apart: see BranchNames.
    public override async Task<PagedResult<SalesQuotationDetailDto>> GetPagedAsync(BaseFilterQuery query, CancellationToken ct = default)
    {
        var page = await base.GetPagedAsync(query, ct);
        await NameBranchesAsync(page.Items, ct);
        return page;
    }
    public override async Task<SalesQuotationDetailDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var dto = await base.GetByIdAsync(id, ct);
        return dto == null ? null : await NamedAsync(dto, ct);
    }
    private async Task<SalesQuotationDetailDto> NamedAsync(SalesQuotationDetailDto dto, CancellationToken ct) { await NameBranchesAsync([dto], ct); return dto; }
    private async Task NameBranchesAsync(IEnumerable<SalesQuotationDetailDto> dtos, CancellationToken ct)
    {
        var names = await BranchNames.ForAsync(Context, dtos.Select(d => d.BranchId), ct);
        foreach (var dto in dtos) dto.BranchName = names.GetValueOrDefault(dto.BranchId);
    }
    protected override SalesQuotation MapToEntity(CreateSalesQuotationRequest request) => new();

    protected override async Task BeforeCreateAsync(SalesQuotation entity, CreateSalesQuotationRequest request, CancellationToken ct)
    {
        await PrepareAsync(entity, request, ct);
        var sequence = await posting.NextValueAsync(ct);
        entity.QuotationNumber = $"BG-{DateTime.UtcNow:yyyyMM}-{sequence:D4}";
        entity.Status = QuotationStatus.Draft;
    }

    protected override async Task BeforeUpdateAsync(SalesQuotation entity, UpdateSalesQuotationRequest request, CancellationToken ct)
    {
        Require(entity.Status is QuotationStatus.Draft or QuotationStatus.Sent, "error.sd.quotation_invalid_status", 409);
        Context.SalesQuotationItems.RemoveRange(entity.Items);
        entity.Items.Clear();
        await PrepareAsync(entity, request, ct);
    }

    protected override void MapUpdateToEntity(UpdateSalesQuotationRequest request, SalesQuotation entity) { }

    protected override Task BeforeDeleteAsync(SalesQuotation entity, CancellationToken ct)
    {
        Require(entity.Status == QuotationStatus.Draft, "error.sd.quotation_invalid_status", 409);
        return Task.CompletedTask;
    }

    // The saved entity has no navigation data yet, so return the freshly loaded detail.
    public override Task<SalesQuotationDetailDto> CreateAsync(CreateSalesQuotationRequest request, CancellationToken ct = default) => posting.RunAsync(async () =>
    {
        var created = await base.CreateAsync(request, ct);
        Context.ChangeTracker.Clear();
        return (await GetByIdAsync(created.Id, ct))!;
    }, ct);

    public override Task<SalesQuotationDetailDto?> UpdateAsync(int id, UpdateSalesQuotationRequest request, CancellationToken ct = default) => posting.RunAsync(async () =>
    {
        await posting.LockAsync($"quotation:{id}", ct);
        var updated = await base.UpdateAsync(id, request, ct);
        if (updated == null) return null;
        Context.ChangeTracker.Clear();
        return await GetByIdAsync(id, ct);
    }, ct);

    public override Task<bool> DeleteAsync(int id, CancellationToken ct = default) => posting.RunAsync(async () =>
    {
        await posting.LockAsync($"quotation:{id}", ct);
        return await base.DeleteAsync(id, ct);
    }, ct);

    public Task<SalesQuotationDetailDto> SendAsync(int id, CancellationToken ct = default) =>
        ChangeStatusAsync(id, [QuotationStatus.Draft], QuotationStatus.Sent, ct);

    public Task<SalesQuotationDetailDto> AcceptAsync(int id, CancellationToken ct = default) =>
        ChangeStatusAsync(id, [QuotationStatus.Sent], QuotationStatus.Accepted, ct);

    public Task<SalesQuotationDetailDto> RejectAsync(int id, CancellationToken ct = default) =>
        ChangeStatusAsync(id, [QuotationStatus.Sent, QuotationStatus.Accepted], QuotationStatus.Rejected, ct);

    public async Task<ConvertQuotationResponse> ConvertToOrderAsync(int id, CancellationToken ct = default)
    {
        DocumentNotice? publish = null;
        var result = await posting.RunAsync(async () =>
    {
        await posting.LockAsync($"quotation:{id}", ct);
        var quotation = await FindEntityAsync(id, ct);
        Require(quotation != null, "error.validation.reference", 404);
        Require(quotation!.Status != QuotationStatus.Converted, "error.sd.quotation_already_converted", 409);
        Require(OpenStatuses.Contains(quotation.Status), "error.sd.quotation_invalid_status", 409);
        Require(quotation.ValidUntil >= DateTime.UtcNow, "error.sd.quotation_expired", 409);

        var order = new SalesOrder
        {
            OrderNumber = await posting.NumberAsync("SO", ct),
            OrderDate = DateTime.UtcNow,
            BranchId = quotation.BranchId,
            DepartmentId = quotation.DepartmentId,
            CustomerId = quotation.CustomerId,
            CustomerName = quotation.CustomerName,
            Status = SalesOrderStatus.Draft,
            QuotationId = quotation.Id,
            SalespersonId = quotation.SalespersonId,
            PriceListId = quotation.PriceListId,
            DeliveryAddressId = quotation.DeliveryAddressId,
            DeliveryRecipientName = quotation.DeliveryRecipientName,
            DeliveryPhone = quotation.DeliveryPhone,
            DeliveryAddressText = quotation.DeliveryAddressText,
            DeliveryAddress = quotation.DeliveryAddressText is { Length: > 500 } addressText ? addressText[..500] : quotation.DeliveryAddressText,
            Notes = quotation.QuotationNumber,
            // Orders carry line discounts inside the line total, so the header discount stays empty.
            SubTotal = quotation.Items.Sum(i => i.LineTotal),
            DiscountAmount = 0m,
            TaxAmount = quotation.TaxAmount,
            Items = quotation.Items.Select(i => new SalesOrderItem
            {
                ItemId = i.ItemId,
                ItemCode = i.ItemCode,
                ItemName = i.ItemName,
                UnitName = i.UnitName,
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
                DiscountAmount = i.DiscountAmount,
                LineTotal = i.LineTotal
            }).ToList()
        };
        order.TotalAmount = order.SubTotal + order.TaxAmount;
        Context.SalesOrders.Add(order);
        await Context.SaveChangesAsync(ct);   // the order id is needed to link back; the two rows point at each other
        quotation.ConvertedSalesOrderId = order.Id;
        quotation.Status = QuotationStatus.Converted;
        quotation.LastModifiedAtUtc = DateTime.UtcNow;
        await Context.SaveChangesAsync(ct);
        publish = QuotationEvent("SD_QUOTATION_CONVERTED", ERPm.Domain.Enums.NotificationSeverity.INFO, quotation, order.OrderNumber);
        return new ConvertQuotationResponse { QuotationId = quotation.Id, SalesOrderId = order.Id, OrderNumber = order.OrderNumber };
    }, ct);
        await PublishAsync(publish, ct);
        return result;
    }

    private async Task<SalesQuotationDetailDto> ChangeStatusAsync(int id, string[] from, string to, CancellationToken ct)
    {
        DocumentNotice? publish = null;
        var dto = await posting.RunAsync(async () =>
    {
        await posting.LockAsync($"quotation:{id}", ct);
        var quotation = await FindEntityAsync(id, ct);
        Require(quotation != null, "error.validation.reference", 404);
        Require(from.Contains(quotation!.Status), "error.sd.quotation_invalid_status", 409);
        Require(quotation.ValidUntil >= DateTime.UtcNow, "error.sd.quotation_expired", 409);
        quotation.Status = to;
        quotation.LastModifiedAtUtc = DateTime.UtcNow;
        // Sending is the seller's own action; the customer's answer is what the team needs to hear about.
        if (to == QuotationStatus.Accepted) publish = QuotationEvent("SD_QUOTATION_ACCEPTED", ERPm.Domain.Enums.NotificationSeverity.INFO, quotation, null);
        if (to == QuotationStatus.Rejected) publish = QuotationEvent("SD_QUOTATION_REJECTED", ERPm.Domain.Enums.NotificationSeverity.WARNING, quotation, null);
        return await NamedAsync(MapToDto(quotation), ct);
    }, ct);
        await PublishAsync(publish, ct);
        return dto;
    }

    private static DocumentNotice QuotationEvent(string template, string severity, SalesQuotation quotation, string? orderCode) => new(
        template, ERPm.Domain.Enums.NotificationCategory.SD, severity, "sd:quotation:view", quotation.BranchId, quotation.DepartmentId, quotation.CreatedBy,
        new Dictionary<string, object>
        {
            ["quotation_code"] = quotation.QuotationNumber,
            ["customer_name"] = quotation.CustomerName,
            ["total_amount"] = quotation.TotalAmount,
            ["order_code"] = orderCode ?? string.Empty
        }, "/sales/quotations");

    // Sent after the transaction committed; only accounts that may view quotations in a scope reaching this one are told.
    private Task PublishAsync(DocumentNotice? notice, CancellationToken ct) => PermissionRecipients.PublishAsync(Context, iam, notificationEngine, currentUser, notice, ct);

    private static string EffectiveStatus(SalesQuotation q) =>
        OpenStatuses.Contains(q.Status) && q.ValidUntil < DateTime.UtcNow ? QuotationStatus.Expired : q.Status;

    private async Task ApplyDeliveryAddressAsync(SalesQuotation entity, int? addressId, int customerId, int? storedAddressId, int storedCustomerId, CancellationToken ct)
    {
        // Keep the quotation's original copy when the same address remains selected, even if its source changed or disappeared.
        if (addressId.HasValue && addressId == storedAddressId && customerId == storedCustomerId && entity.DeliveryAddressText != null)
            return;

        entity.DeliveryAddressId = null;
        entity.DeliveryRecipientName = null;
        entity.DeliveryPhone = null;
        entity.DeliveryAddressText = null;
        if (!addressId.HasValue) return;

        var address = await Context.PartnerDeliveryAddresses.AsNoTracking()
            .SingleOrDefaultAsync(a => a.Id == addressId.Value && a.PartnerId == customerId, ct);
        Require(address != null, "error.sd.order_delivery_address_invalid");
        entity.DeliveryAddressId = address!.Id;
        entity.DeliveryRecipientName = address.RecipientName;
        entity.DeliveryPhone = address.Phone;
        var addressText = string.Join(", ", new[] { address.Address, address.Ward, address.District, address.City }
            .Where(part => !string.IsNullOrWhiteSpace(part)));
        entity.DeliveryAddressText = addressText.Length <= 800 ? addressText : addressText[..800];
    }

    private async Task PrepareAsync(SalesQuotation entity, CreateSalesQuotationRequest request, CancellationToken ct)
    {
        var storedAddressId = entity.DeliveryAddressId;
        var storedCustomerId = entity.CustomerId;
        Require(request.Items is { Count: > 0 and <= MaxLines }, "error.validation.request");
        Require(request.QuotationDate.Kind == DateTimeKind.Utc && request.ValidUntil.Kind == DateTimeKind.Utc && request.ValidUntil >= request.QuotationDate, "error.validation.request");
        // A seller limited to their own records has no branch in their data scope, so the branch is checked outside it.
        Require(await Context.Branches.IgnoreQueryFilters().AnyAsync(b => !b.IsDeleted && b.Id == request.BranchId && b.IsActive, ct), "error.validation.request");
        Require(request.DepartmentId == null || await Context.Departments.AnyAsync(d => d.Id == request.DepartmentId, ct), "error.validation.reference");
        Require(request.SalespersonId == null || await Context.Employees.AnyAsync(e => e.Id == request.SalespersonId && e.IsActive, ct), "error.validation.reference");
        Require(request.PriceListId == null || await Context.PriceLists.AnyAsync(p => p.Id == request.PriceListId, ct), "error.validation.reference");
        var customer = await Context.Partners.SingleOrDefaultAsync(p => p.Id == request.CustomerId && p.IsActive, ct);
        Require(customer != null && customer.PartnerType.HasFlag(PartnerType.Customer), "error.sales.customer");

        var ids = request.Items.Select(i => i.ItemId).Distinct().ToArray();
        var items = await Context.Items.Include(i => i.BaseUom).Where(i => ids.Contains(i.Id) && i.IsActive).ToDictionaryAsync(i => i.Id, ct);
        Require(items.Count == ids.Length, "error.inventory.item");

        var lines = request.Items.Select(line =>
        {
            ValidateNumber(line.Quantity, true); ValidateNumber(line.UnitPrice); ValidateNumber(line.DiscountAmount);
            var gross = Round(line.Quantity * line.UnitPrice);
            Require(line.DiscountAmount <= gross, "error.sales.discount");
            var item = items[line.ItemId];
            return new SalesQuotationItem
            {
                ItemId = item.Id, ItemCode = item.Code, ItemName = item.Name, UnitName = item.BaseUom?.Name,
                Quantity = line.Quantity, UnitPrice = line.UnitPrice, DiscountAmount = line.DiscountAmount,
                LineTotal = gross - line.DiscountAmount, Notes = line.Notes
            };
        }).ToList();
        foreach (var line in lines) entity.Items.Add(line);

        entity.CustomerId = customer!.Id;
        entity.CustomerName = customer.Name;
        entity.CustomerPhone = customer.Phone;
        entity.CustomerEmail = customer.Email;
        await ApplyDeliveryAddressAsync(entity, request.DeliveryAddressId, customer.Id, storedAddressId, storedCustomerId, ct);
        entity.SalespersonId = request.SalespersonId;
        entity.BranchId = request.BranchId;
        entity.DepartmentId = request.DepartmentId;
        entity.PriceListId = request.PriceListId;
        entity.QuotationDate = request.QuotationDate;
        entity.ValidUntil = request.ValidUntil;
        entity.TermsAndConditions = request.TermsAndConditions;
        entity.SubTotal = lines.Sum(l => Round(l.Quantity * l.UnitPrice));
        entity.DiscountAmount = lines.Sum(l => l.DiscountAmount);
        entity.TaxAmount = 0m;
        entity.TotalAmount = entity.SubTotal - entity.DiscountAmount + entity.TaxAmount;
        ValidateNumber(entity.TotalAmount);
    }
}
