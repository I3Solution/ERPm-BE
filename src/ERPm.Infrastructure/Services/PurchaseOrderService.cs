using ERPm.Application.Common.Crud;
using ERPm.Application.DTOs.Purchase;
using ERPm.Domain.Entities.Purchase;
using ERPm.Domain.Entities.Partner;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;
using static ERPm.Infrastructure.Services.Common.PostingTransaction;
namespace ERPm.Infrastructure.Services;
public sealed class PurchaseOrderService(ApplicationDbContext context, PostingTransaction posting)
    : BaseCrudService<PurchaseOrder, PurchaseOrderDto, CreatePurchaseOrderRequest, UpdatePurchaseOrderRequest>(context), IOrderWorkflow<PurchaseOrderDto>
{
    protected override IQueryable<PurchaseOrder> PrepareQuery(IQueryable<PurchaseOrder> query) => query.Include(o => o.Items);
    protected override Task<PurchaseOrder?> FindEntityAsync(int id, CancellationToken ct) => Context.Set<PurchaseOrder>().Include(o => o.Items).SingleOrDefaultAsync(o => o.Id == id, ct);
    protected override async Task BeforeCreateAsync(PurchaseOrder entity, CreatePurchaseOrderRequest request, CancellationToken ct)
    {
        await PrepareAsync(entity, request, ct);
        entity.OrderNumber = string.IsNullOrWhiteSpace(request.OrderNumber) ? await posting.NumberAsync("PO", ct) : request.OrderNumber.Trim();
        entity.Status = "Draft";
    }
    protected override async Task BeforeUpdateAsync(PurchaseOrder entity, UpdatePurchaseOrderRequest request, CancellationToken ct)
    {
        Require(entity.Status == "Draft" && (request.Status == null || request.Status == "Draft"), "error.order.state", 409);
        Context.RemoveRange(entity.Items);
        await PrepareAsync(entity, request, ct);
    }
    protected override void MapUpdateToEntity(UpdatePurchaseOrderRequest request, PurchaseOrder entity) { }
    protected override Task BeforeDeleteAsync(PurchaseOrder entity, CancellationToken ct)
    {
        Require(entity.Status == "Draft", "error.order.state", 409);
        return Task.CompletedTask;
    }
    public override Task<PurchaseOrderDto?> UpdateAsync(int id, UpdatePurchaseOrderRequest request, CancellationToken ct = default) => posting.RunAsync(async () =>
    {
        await posting.LockAsync($"order:Purchase:{id}", ct);
        return await base.UpdateAsync(id, request, ct);
    }, ct);
    public override Task<bool> DeleteAsync(int id, CancellationToken ct = default) => posting.RunAsync(async () =>
    {
        await posting.LockAsync($"order:Purchase:{id}", ct);
        return await base.DeleteAsync(id, ct);
    }, ct);
    public Task<PurchaseOrderDto> ConfirmAsync(int id, CancellationToken ct) => posting.RunAsync(async () =>
    {
        await posting.LockAsync($"order:Purchase:{id}", ct);
        var entity = await FindEntityAsync(id, ct);
        Require(entity != null, "error.order.not_found", 404);
        Require(entity!.Status == "Draft", "error.order.state", 409);
        entity.Status = "Confirmed";
        await Context.SaveChangesAsync(ct);
        return MapToDto(entity);
    }, ct);
    private async Task PrepareAsync(PurchaseOrder entity, CreatePurchaseOrderRequest request, CancellationToken ct)
    {
        Require(request.Items is { Count: > 0 and <= 200 } && await Context.Branches.AnyAsync(b => b.Id == request.BranchId && b.IsActive, ct), "error.validation.request");
        Require(request.PaidAmount == 0, "error.validation.request");
        ValidateNumber(0m); ValidateNumber(request.TaxAmount);
        var partner = request.SupplierId.HasValue ? await Context.Partners.SingleOrDefaultAsync(p => p.Id == request.SupplierId && p.IsActive, ct) : null;
        Require(partner != null && partner.PartnerType.HasFlag(PartnerType.Supplier), "error.sales.customer");
        var ids = request.Items.Select(i => i.ItemId).Distinct().ToArray();
        var items = await Context.Items.Include(i => i.BaseUom).Where(i => ids.Contains(i.Id) && i.IsActive).ToDictionaryAsync(i => i.Id, ct);
        Require(items.Count == ids.Length, "error.inventory.item");
        entity.Items = request.Items.Select(line =>
        {
            ValidateNumber(line.Quantity, true); ValidateNumber(line.UnitPrice);
            var discount = 0m; ValidateNumber(discount);
            var amount = Round(line.Quantity * line.UnitPrice) - discount;
            Require(amount >= 0, "error.sales.discount");
            var item = items[line.ItemId];
            return new PurchaseOrderItem { ItemId = item.Id, ItemCode = item.Code, ItemName = item.Name, UnitName = item.BaseUom?.Name, Quantity = line.Quantity, UnitPrice = line.UnitPrice, LineTotal = amount };
        }).ToList();
        entity.BranchId = request.BranchId; entity.SupplierId = partner?.Id; entity.SupplierName = partner?.Name ?? request.SupplierName.Trim();
        entity.SubTotal = entity.Items.Sum(i => i.LineTotal);
        Require(0m <= entity.SubTotal, "error.sales.discount");

        entity.TaxAmount = request.TaxAmount; entity.TotalAmount = entity.SubTotal - 0m + request.TaxAmount;
        ValidateNumber(entity.TotalAmount, true); entity.Notes = request.Notes;
        if (request.OrderDate.HasValue) { Require(request.OrderDate.Value.Kind == DateTimeKind.Utc, "error.validation.request"); entity.OrderDate = request.OrderDate.Value; }
    }
}
