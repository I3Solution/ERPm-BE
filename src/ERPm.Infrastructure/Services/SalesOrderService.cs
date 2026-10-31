using ERPm.Application.Common.Crud;
using ERPm.Application.DTOs.Sales;
using ERPm.Domain.Entities.Sales;
using ERPm.Domain.Entities.Partner;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;
using static ERPm.Infrastructure.Services.Common.PostingTransaction;
namespace ERPm.Infrastructure.Services;
public sealed class SalesOrderService(ApplicationDbContext context, PostingTransaction posting, ERPm.Application.Common.Notifications.INotificationEngine? notificationEngine = null)
    : BaseCrudService<SalesOrder, SalesOrderDto, CreateSalesOrderRequest, UpdateSalesOrderRequest>(context), IOrderWorkflow<SalesOrderDto>
{
    protected override IQueryable<SalesOrder> PrepareQuery(IQueryable<SalesOrder> query) => query.Include(o => o.Items);
    protected override Task<SalesOrder?> FindEntityAsync(int id, CancellationToken ct) => Context.Set<SalesOrder>().Include(o => o.Items).SingleOrDefaultAsync(o => o.Id == id, ct);
    protected override async Task BeforeCreateAsync(SalesOrder entity, CreateSalesOrderRequest request, CancellationToken ct)
    {
        await PrepareAsync(entity, request, ct);
        entity.OrderNumber = string.IsNullOrWhiteSpace(request.OrderNumber) ? await posting.NumberAsync("SO", ct) : request.OrderNumber.Trim();
        entity.Status = "Draft";
    }
    protected override async Task BeforeUpdateAsync(SalesOrder entity, UpdateSalesOrderRequest request, CancellationToken ct)
    {
        Require(entity.Status == "Draft" && (request.Status == null || request.Status == "Draft"), "error.order.state", 409);
        Context.RemoveRange(entity.Items);
        await PrepareAsync(entity, request, ct);
    }
    protected override void MapUpdateToEntity(UpdateSalesOrderRequest request, SalesOrder entity) { }
    protected override Task BeforeDeleteAsync(SalesOrder entity, CancellationToken ct)
    {
        Require(entity.Status == "Draft", "error.order.state", 409);
        return Task.CompletedTask;
    }
    public override Task<SalesOrderDto?> UpdateAsync(int id, UpdateSalesOrderRequest request, CancellationToken ct = default) => posting.RunAsync(async () =>
    {
        await posting.LockAsync($"order:Sales:{id}", ct);
        return await base.UpdateAsync(id, request, ct);
    }, ct);
    public override Task<bool> DeleteAsync(int id, CancellationToken ct = default) => posting.RunAsync(async () =>
    {
        await posting.LockAsync($"order:Sales:{id}", ct);
        return await base.DeleteAsync(id, ct);
    }, ct);
    public Task<SalesOrderDto> ConfirmAsync(int id, CancellationToken ct) => posting.RunAsync(async () =>
    {
        await posting.LockAsync($"order:Sales:{id}", ct);
        var entity = await FindEntityAsync(id, ct);
        Require(entity != null, "error.order.not_found", 404);
        Require(entity!.Status == "Draft", "error.order.state", 409);
        entity.Status = "Confirmed";
        await Context.SaveChangesAsync(ct);

        if (notificationEngine != null)
        {
            var customerName = entity.CustomerId.HasValue
                ? await Context.Set<Partner>().Where(p => p.Id == entity.CustomerId).Select(p => p.Name).FirstOrDefaultAsync(ct)
                : "Khách lẻ";

            _ = notificationEngine.PublishAsync(new ERPm.Domain.Events.NotificationEvent(
                "SD_ORDER_NEW",
                ERPm.Domain.Enums.NotificationCategory.SD,
                ERPm.Domain.Enums.NotificationSeverity.INFO,
                ERPm.Domain.Enums.NotificationTargetType.BranchTree,
                branchId: entity.BranchId,
                parameters: new Dictionary<string, object>
                {
                    ["order_code"] = entity.OrderNumber,
                    ["customer_name"] = customerName ?? "Khách lẻ",
                    ["total_amount"] = entity.TotalAmount
                },
                actionUrl: "/sales/pos"
            ));
        }
        return MapToDto(entity);
    }, ct);
    private async Task PrepareAsync(SalesOrder entity, CreateSalesOrderRequest request, CancellationToken ct)
    {
        Require(request.Items is { Count: > 0 and <= 200 } && await Context.Branches.AnyAsync(b => b.Id == request.BranchId && b.IsActive, ct), "error.validation.request");
        Require(request.PaidAmount == 0, "error.validation.request");
        ValidateNumber(request.DiscountAmount); ValidateNumber(request.TaxAmount);
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
            return new SalesOrderItem { ItemId = item.Id, ItemCode = item.Code, ItemName = item.Name, UnitName = item.BaseUom?.Name, Quantity = line.Quantity, UnitPrice = line.UnitPrice, DiscountAmount = discount, LineTotal = amount };
        }).ToList();
        entity.BranchId = request.BranchId; entity.CustomerId = partner?.Id; entity.CustomerName = partner?.Name ?? request.CustomerName.Trim();
        entity.SubTotal = entity.Items.Sum(i => i.LineTotal);
        Require(request.DiscountAmount <= entity.SubTotal, "error.sales.discount");
        entity.DiscountAmount = request.DiscountAmount;
        entity.TaxAmount = request.TaxAmount; entity.TotalAmount = entity.SubTotal - request.DiscountAmount + request.TaxAmount;
        ValidateNumber(entity.TotalAmount, true); entity.Notes = request.Notes;
        if (request.OrderDate.HasValue) { Require(request.OrderDate.Value.Kind == DateTimeKind.Utc, "error.validation.request"); entity.OrderDate = request.OrderDate.Value; }
    }
}
