using ERPm.Application.Common.Crud;
using ERPm.Application.DTOs.Sales;
using ERPm.Domain.Entities.Partner;
using ERPm.Domain.Entities.Sales;
using ERPm.Domain.Services.Sales;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;
using static ERPm.Infrastructure.Services.Common.PostingTransaction;

namespace ERPm.Infrastructure.Services;

/// <summary>Price list CRUD plus the pricing engine entry point used by quotations and orders.</summary>
public sealed class PriceListService(ApplicationDbContext context)
    : BaseCrudService<PriceList, PriceListDetailDto, CreatePriceListRequest, UpdatePriceListRequest>(context), IPricingService
{
    private const int MaxLines = 1000;

    protected override IQueryable<PriceList> PrepareQuery(IQueryable<PriceList> query) => query.Include(p => p.Branch);

    protected override Task<PriceList?> FindEntityAsync(int id, CancellationToken ct) => Context.PriceLists
        .Include(p => p.Branch)
        .Include(p => p.Items).ThenInclude(i => i.Item).ThenInclude(i => i!.BaseUom)
        .SingleOrDefaultAsync(p => p.Id == id, ct);

    protected override PriceListDetailDto MapToDto(PriceList entity) => new()
    {
        Id = entity.Id,
        ExternalId = entity.ExternalId,
        Code = entity.Code,
        Name = entity.Name,
        Currency = entity.Currency,
        EffectiveFrom = entity.EffectiveFrom,
        EffectiveTo = entity.EffectiveTo,
        BranchId = entity.BranchId,
        BranchName = entity.Branch?.Name,
        IsActive = entity.IsActive,
        Notes = entity.Notes,
        Items = entity.Items
            .OrderBy(i => i.Item?.Code).ThenBy(i => i.MinQuantity)
            .Select(i => new PriceListItemDto
            {
                Id = i.Id,
                ItemId = i.ItemId,
                ItemCode = i.Item?.Code ?? string.Empty,
                ItemName = i.Item?.Name ?? string.Empty,
                UnitName = i.Item?.BaseUom?.Name,
                UnitPrice = i.UnitPrice,
                MinQuantity = i.MinQuantity,
                DiscountPercent = i.DiscountPercent
            }).ToList()
    };

    protected override PriceList MapToEntity(CreatePriceListRequest request) => new()
    {
        Items = request.Items.Select(l => new PriceListItem { ItemId = l.ItemId, UnitPrice = l.UnitPrice, MinQuantity = l.MinQuantity, DiscountPercent = l.DiscountPercent }).ToList()
    };

    protected override async Task BeforeCreateAsync(PriceList entity, CreatePriceListRequest request, CancellationToken ct)
    {
        await ValidateAsync(request, null, ct);
        ApplyHeader(request, entity);
    }

    protected override async Task BeforeUpdateAsync(PriceList entity, UpdatePriceListRequest request, CancellationToken ct)
    {
        await ValidateAsync(request, entity.Id, ct);
        // Sync lines by (item, tier) instead of replacing them, so a re-saved list never collides with its own unique index.
        var wanted = request.Items.ToDictionary(l => (l.ItemId, l.MinQuantity));
        foreach (var existing in entity.Items.ToList())
        {
            if (wanted.Remove((existing.ItemId, existing.MinQuantity), out var line))
            {
                existing.UnitPrice = line.UnitPrice;
                existing.DiscountPercent = line.DiscountPercent;
            }
            else
            {
                Context.PriceListItems.Remove(existing);
            }
        }
        foreach (var line in wanted.Values)
            entity.Items.Add(new PriceListItem { ItemId = line.ItemId, UnitPrice = line.UnitPrice, MinQuantity = line.MinQuantity, DiscountPercent = line.DiscountPercent });
    }

    protected override void MapUpdateToEntity(UpdatePriceListRequest request, PriceList entity) => ApplyHeader(request, entity);

    protected override async Task BeforeDeleteAsync(PriceList entity, CancellationToken ct)
    {
        var inUse = await Context.Partners.IgnoreQueryFilters().AnyAsync(p => !p.IsDeleted && p.PriceListId == entity.Id, ct)
            || await Context.SalesOrders.IgnoreQueryFilters().AnyAsync(o => !o.IsDeleted && o.PriceListId == entity.Id, ct);
        Require(!inUse, "error.sd.pricelist_in_use", 409);
    }

    // The saved entity has no navigation data yet, so return the freshly loaded detail.
    public override async Task<PriceListDetailDto> CreateAsync(CreatePriceListRequest request, CancellationToken ct = default)
    {
        var created = await base.CreateAsync(request, ct);
        Context.ChangeTracker.Clear();
        return (await GetByIdAsync(created.Id, ct))!;
    }

    public override async Task<PriceListDetailDto?> UpdateAsync(int id, UpdatePriceListRequest request, CancellationToken ct = default)
    {
        var updated = await base.UpdateAsync(id, request, ct);
        if (updated == null) return null;
        Context.ChangeTracker.Clear();
        return await GetByIdAsync(id, ct);
    }

    public async Task<CalculatePriceResponse> CalculateAsync(CalculatePriceRequest request, CancellationToken ct = default)
    {
        // Pricing only reads prices, so the branch is checked outside the caller's data scope: a SELF-scoped seller has no branches of their own.
        Require(request.Items is { Count: > 0 and <= 200 } && await Context.Branches.IgnoreQueryFilters().AnyAsync(b => !b.IsDeleted && b.Id == request.BranchId && b.IsActive, ct), "error.validation.request");
        foreach (var line in request.Items) ValidateNumber(line.Quantity, true);

        int? customerListId = null;
        if (request.CustomerId.HasValue)
        {
            var customer = await Context.Partners.Where(p => p.Id == request.CustomerId && p.IsActive)
                .Select(p => new { p.PartnerType, p.PriceListId }).SingleOrDefaultAsync(ct);
            Require(customer != null && customer.PartnerType.HasFlag(PartnerType.Customer), "error.sales.customer");
            customerListId = customer!.PriceListId;
        }

        var itemIds = request.Items.Select(i => i.ItemId).Distinct().ToArray();
        var catalog = await Context.Items.Where(i => itemIds.Contains(i.Id) && i.IsActive).ToDictionaryAsync(i => i.Id, i => i.SalePrice, ct);
        Require(catalog.Count == itemIds.Length, "error.inventory.item");

        var now = DateTime.UtcNow;
        var lists = await Context.PriceLists
            .Where(p => p.IsActive && p.EffectiveFrom <= now && (p.EffectiveTo == null || p.EffectiveTo >= now) && (p.BranchId == null || p.BranchId == request.BranchId))
            .Select(p => new { p.Id, p.Name, p.BranchId, p.EffectiveFrom })
            .ToListAsync(ct);
        var listIds = lists.Select(p => p.Id).ToArray();
        // Only lines priced in the item's base unit take part until unit conversion is priced (UomId is null then).
        var lines = await Context.PriceListItems
            .Where(l => listIds.Contains(l.PriceListId) && itemIds.Contains(l.ItemId) && l.UomId == null)
            .Select(l => new { l.PriceListId, l.ItemId, Tier = new PriceTier(l.MinQuantity, l.UnitPrice, l.DiscountPercent) })
            .ToListAsync(ct);

        var response = new CalculatePriceResponse();
        foreach (var line in request.Items)
        {
            var candidates = lists
                .Select(p => new PriceCandidate(p.Id, p.Name, p.Id == customerListId, p.BranchId != null, p.EffectiveFrom,
                    lines.Where(l => l.PriceListId == p.Id && l.ItemId == line.ItemId).Select(l => l.Tier).ToList()))
                .Where(c => c.Tiers.Count > 0);
            var result = PricingEngine.Price(catalog[line.ItemId], line.Quantity, candidates);
            response.Items.Add(new CalculatePriceLineDto
            {
                ItemId = line.ItemId,
                AppliedPriceListId = result.AppliedPriceListId,
                AppliedPriceListName = result.AppliedPriceListName,
                OriginalPrice = result.OriginalPrice,
                UnitPrice = result.UnitPrice,
                DiscountPercent = result.DiscountPercent,
                DiscountAmount = result.DiscountAmount,
                LineTotal = result.LineTotal
            });
        }
        return response;
    }

    private static void ApplyHeader(CreatePriceListRequest request, PriceList entity)
    {
        entity.Code = request.Code.Trim().ToUpperInvariant();
        entity.Name = request.Name.Trim();
        entity.Currency = request.Currency.Trim().ToUpperInvariant();
        entity.EffectiveFrom = request.EffectiveFrom;
        entity.EffectiveTo = request.EffectiveTo;
        entity.BranchId = request.BranchId;
        entity.IsActive = request.IsActive;
        entity.Notes = request.Notes;
    }

    private async Task ValidateAsync(CreatePriceListRequest request, int? selfId, CancellationToken ct)
    {
        var code = request.Code?.Trim() ?? string.Empty;
        Require(code.Length is > 0 and <= 50 && !string.IsNullOrWhiteSpace(request.Name) && request.Name.Trim().Length <= 255, "error.validation.request");
        Require(request.Currency?.Trim().Length == 3, "error.validation.request");
        Require(request.EffectiveFrom.Kind == DateTimeKind.Utc && (request.EffectiveTo == null || request.EffectiveTo.Value.Kind == DateTimeKind.Utc && request.EffectiveTo >= request.EffectiveFrom), "error.validation.request");
        Require(request.BranchId == null || await Context.Branches.IgnoreQueryFilters().AnyAsync(b => !b.IsDeleted && b.Id == request.BranchId && b.IsActive, ct), "error.validation.reference");
        var normalized = code.ToUpperInvariant();
        Require(!await Context.PriceLists.AnyAsync(p => p.Code == normalized && p.Id != selfId, ct), "error.sd.pricelist_duplicate_code", 409);

        Require(request.Items is { Count: <= MaxLines }, "error.validation.request");
        foreach (var line in request.Items)
        {
            ValidateNumber(line.UnitPrice); ValidateNumber(line.MinQuantity); ValidateNumber(line.DiscountPercent);
            Require(line.DiscountPercent <= 100, "error.validation.number");
        }
        Require(request.Items.Select(l => (l.ItemId, l.MinQuantity)).Distinct().Count() == request.Items.Count, "error.validation.request");
        var itemIds = request.Items.Select(l => l.ItemId).Distinct().ToArray();
        Require(await Context.Items.CountAsync(i => itemIds.Contains(i.Id) && i.IsActive, ct) == itemIds.Length, "error.inventory.item");
    }
}
