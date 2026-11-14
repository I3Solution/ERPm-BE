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

public sealed class PriceListService(ApplicationDbContext context)
    : BaseCrudService<PriceList, PriceListDetailDto, CreatePriceListRequest, UpdatePriceListRequest>(context), IPricingService
{
    private const int MaxLines = 1000;

    protected override IQueryable<PriceList> PrepareQuery(IQueryable<PriceList> query) => query.Include(p => p.Branch);

    protected override Task<PriceList?> FindEntityAsync(int id, CancellationToken ct) => Context.PriceLists
        .Include(p => p.Branch)
        .Include(p => p.Versions).ThenInclude(v => v.Items).ThenInclude(i => i.Item).ThenInclude(i => i!.BaseUom)
        .SingleOrDefaultAsync(p => p.Id == id, ct);

    private static PriceListVersion? CurrentVersion(PriceList list) => list.Versions
        .Where(v => v.Status == PriceListVersionStatus.Active)
        .OrderByDescending(v => v.EffectiveFrom).ThenByDescending(v => v.Id)
        .FirstOrDefault();

    private static PriceListVersion NewDefaultVersion(PriceList list) => new()
    {
        VersionCode = "V1",
        Status = PriceListVersionStatus.Active,
        EffectiveFrom = list.EffectiveFrom,
        EffectiveTo = list.EffectiveTo
    };

    protected override PriceListDetailDto MapToDto(PriceList entity) => new()
    {
        Id = entity.Id,
        ExternalId = entity.ExternalId,
        Code = entity.Code,
        Name = entity.Name,
        Currency = entity.Currency,
        Type = entity.Type,
        IsTaxInclusive = entity.IsTaxInclusive,
        EffectiveFrom = entity.EffectiveFrom,
        EffectiveTo = entity.EffectiveTo,
        BranchId = entity.BranchId,
        BranchName = entity.Branch?.Name,
        IsActive = entity.IsActive,
        Notes = entity.Notes,
        Items = (CurrentVersion(entity)?.Items ?? Array.Empty<PriceListItem>())
            .OrderBy(i => i.Item?.Code).ThenBy(i => i.MinQuantity)
            .Select(MapLine)
            .ToList()
    };

    private static PriceListItemDto MapLine(PriceListItem item) => new()
    {
        Id = item.Id,
        ItemId = item.ItemId,
        ItemCode = item.Item?.Code ?? string.Empty,
        ItemName = item.Item?.Name ?? string.Empty,
        UnitName = item.Item?.BaseUom?.Name,
        UnitPrice = item.UnitPrice,
        MinQuantity = item.MinQuantity,
        DiscountPercent = item.DiscountPercent
    };

    private static PriceListVersionDto MapVersion(PriceListVersion version) => new()
    {
        Id = version.Id,
        PriceListId = version.PriceListId,
        VersionCode = version.VersionCode,
        EffectiveFrom = version.EffectiveFrom,
        EffectiveTo = version.EffectiveTo,
        Status = version.Status,
        ItemCount = version.Items.Count,
        Items = version.Items.OrderBy(i => i.Item?.Code).ThenBy(i => i.MinQuantity).Select(MapLine).ToList()
    };

    protected override PriceList MapToEntity(CreatePriceListRequest request) => new()
    {
        Versions =
        {
            new PriceListVersion
            {
                VersionCode = "V1",
                Status = PriceListVersionStatus.Active,
                Items = request.Items.Select(l => new PriceListItem { ItemId = l.ItemId, UnitPrice = l.UnitPrice, MinQuantity = l.MinQuantity, DiscountPercent = l.DiscountPercent }).ToList()
            }
        }
    };

    protected override async Task BeforeCreateAsync(PriceList entity, CreatePriceListRequest request, CancellationToken ct)
    {
        await ValidateAsync(request, null, ct);
        ApplyHeader(request, entity);
        SyncVersionDates(entity);
    }

    protected override async Task BeforeUpdateAsync(PriceList entity, UpdatePriceListRequest request, CancellationToken ct)
    {
        await ValidateAsync(request, entity.Id, ct);
        var version = CurrentVersion(entity);
        if (version == null)
        {
            version = NewDefaultVersion(entity);
            entity.Versions.Add(version);
        }

        var wanted = request.Items.ToDictionary(l => (l.ItemId, l.MinQuantity));
        foreach (var existing in version.Items.ToList())
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
            version.Items.Add(new PriceListItem { ItemId = line.ItemId, UnitPrice = line.UnitPrice, MinQuantity = line.MinQuantity, DiscountPercent = line.DiscountPercent });
    }

    protected override void MapUpdateToEntity(UpdatePriceListRequest request, PriceList entity)
    {
        ApplyHeader(request, entity);
        SyncVersionDates(entity);
    }

    private static void SyncVersionDates(PriceList entity)
    {
        var version = CurrentVersion(entity);
        if (version == null) return;
        version.EffectiveFrom = entity.EffectiveFrom;
        version.EffectiveTo = entity.EffectiveTo;
    }

    protected override async Task BeforeDeleteAsync(PriceList entity, CancellationToken ct)
    {
        var inUse = await Context.Partners.IgnoreQueryFilters().AnyAsync(p => !p.IsDeleted && p.PriceListId == entity.Id, ct)
            || await Context.SalesOrders.IgnoreQueryFilters().AnyAsync(o => !o.IsDeleted && o.PriceListId == entity.Id, ct);
        Require(!inUse, "error.sd.pricelist_in_use", 409);
    }

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
        Require(request.Items is { Count: > 0 and <= 200 }
            && await Context.Branches.IgnoreQueryFilters().AnyAsync(b => !b.IsDeleted && b.Id == request.BranchId && b.IsActive, ct),
            "error.validation.request");
        foreach (var line in request.Items) ValidateNumber(line.Quantity, true);

        var documentDate = request.DocumentDate ?? DateTime.UtcNow;
        Require(documentDate.Kind == DateTimeKind.Utc, "error.validation.request");

        int? customerListId = null;
        if (request.CustomerId.HasValue)
        {
            var customer = await Context.Partners.Where(p => p.Id == request.CustomerId && p.IsActive)
                .Select(p => new { p.PartnerType, p.PriceListId }).SingleOrDefaultAsync(ct);
            Require(customer != null && customer.PartnerType.HasFlag(PartnerType.Customer), "error.sales.customer");
            customerListId = customer!.PriceListId;
        }

        var itemIds = request.Items.Select(i => i.ItemId).Distinct().ToArray();
        var catalog = await Context.Items.Where(i => itemIds.Contains(i.Id) && i.IsActive)
            .ToDictionaryAsync(i => i.Id, i => new { i.SalePrice, i.VatRate }, ct);
        Require(catalog.Count == itemIds.Length, "error.inventory.item");
        foreach (var item in catalog.Values) Require(item.VatRate is >= 0m and <= 100m, "error.validation.number");

        var eligibleVersions = await Context.PriceListVersions
            .Where(v => v.Status == PriceListVersionStatus.Active
                && v.EffectiveFrom <= documentDate && (v.EffectiveTo == null || v.EffectiveTo >= documentDate)
                && v.PriceList != null && v.PriceList.Type == PriceListType.Sales && v.PriceList.IsActive
                && v.PriceList.EffectiveFrom <= documentDate && (v.PriceList.EffectiveTo == null || v.PriceList.EffectiveTo >= documentDate)
                && (v.PriceList.BranchId == null || v.PriceList.BranchId == request.BranchId))
            .Select(v => new
            {
                VersionId = v.Id,
                v.VersionCode,
                v.EffectiveFrom,
                PriceListId = v.PriceListId,
                PriceListName = v.PriceList!.Name,
                BranchId = v.PriceList!.BranchId,
                PriceListEffectiveFrom = v.PriceList!.EffectiveFrom,
                v.PriceList!.IsTaxInclusive
            })
            .ToListAsync(ct);

        // If multiple active ranges overlap, the latest-starting version wins deterministically.
        var selectedVersions = eligibleVersions
            .GroupBy(v => v.PriceListId)
            .Select(g => g.OrderByDescending(v => v.EffectiveFrom).ThenByDescending(v => v.VersionId).First())
            .ToList();
        var versionIds = selectedVersions.Select(v => v.VersionId).ToArray();
        var lines = await Context.PriceListItems
            .Where(l => versionIds.Contains(l.PriceListVersionId) && itemIds.Contains(l.ItemId) && l.UomId == null)
            .Select(l => new { l.PriceListVersionId, l.ItemId, Tier = new PriceTier(l.MinQuantity, l.UnitPrice, l.DiscountPercent) })
            .ToListAsync(ct);

        var response = new CalculatePriceResponse { DocumentDate = documentDate };
        foreach (var line in request.Items)
        {
            var candidates = selectedVersions
                .Select(v => new PriceCandidate(
                    v.PriceListId,
                    v.PriceListName,
                    v.PriceListId == customerListId,
                    v.BranchId != null,
                    v.PriceListEffectiveFrom,
                    lines.Where(l => l.PriceListVersionId == v.VersionId && l.ItemId == line.ItemId).Select(l => l.Tier).ToList(),
                    v.VersionId,
                    v.VersionCode,
                    v.IsTaxInclusive))
                .Where(c => c.Tiers.Count > 0);
            var item = catalog[line.ItemId];
            var result = PricingEngine.Price(item.SalePrice, line.Quantity, candidates, item.VatRate);
            response.Items.Add(new CalculatePriceLineDto
            {
                ItemId = line.ItemId,
                AppliedPriceListId = result.AppliedPriceListId,
                AppliedPriceListName = result.AppliedPriceListName,
                AppliedPriceListVersionId = result.AppliedPriceListVersionId,
                AppliedVersionCode = result.AppliedVersionCode,
                OriginalPrice = result.OriginalPrice,
                UnitPrice = result.UnitPrice,
                DiscountPercent = result.DiscountPercent,
                DiscountAmount = result.DiscountAmount,
                LineTotal = result.LineTotal,
                VatRate = result.VatRate,
                TaxAmount = result.TaxAmount,
                TotalWithTax = result.TotalWithTax,
                IsTaxInclusive = result.IsTaxInclusive
            });
        }
        return response;
    }

    public async Task<List<PriceListVersionDto>> GetVersionsAsync(int priceListId, CancellationToken ct = default)
    {
        Require(await Context.PriceLists.AnyAsync(p => p.Id == priceListId, ct), "error.validation.reference", 404);
        return await Context.PriceListVersions
            .AsNoTracking()
            .Where(v => v.PriceListId == priceListId)
            .OrderByDescending(v => v.EffectiveFrom)
            .ThenByDescending(v => v.Id)
            .Select(v => new PriceListVersionDto
            {
                Id = v.Id,
                PriceListId = v.PriceListId,
                VersionCode = v.VersionCode,
                EffectiveFrom = v.EffectiveFrom,
                EffectiveTo = v.EffectiveTo,
                Status = v.Status,
                ItemCount = v.Items.Count
            })
            .ToListAsync(ct);
    }

    public async Task<PriceListVersionDto> GetVersionAsync(int priceListId, int versionId, CancellationToken ct = default)
    {
        var version = await Context.PriceListVersions
            .AsNoTracking()
            .Include(v => v.Items).ThenInclude(i => i.Item).ThenInclude(i => i!.BaseUom)
            .SingleOrDefaultAsync(v => v.Id == versionId && v.PriceListId == priceListId, ct);
        Require(version != null, "error.validation.reference", 404);
        return MapVersion(version!);
    }

    public async Task<PriceListVersionDto> CloneVersionAsync(int priceListId, ClonePriceListVersionRequest request, CancellationToken ct = default)
    {
        var code = request.VersionCode?.Trim().ToUpperInvariant() ?? string.Empty;
        Require(code.Length is > 0 and <= 50
            && request.EffectiveFrom.Kind == DateTimeKind.Utc
            && (request.EffectiveTo == null || request.EffectiveTo.Value.Kind == DateTimeKind.Utc && request.EffectiveTo >= request.EffectiveFrom),
            "error.validation.request");
        var source = await Context.PriceListVersions
            .Include(v => v.Items)
            .SingleOrDefaultAsync(v => v.Id == request.SourceVersionId && v.PriceListId == priceListId, ct);
        Require(source != null && source.Status != PriceListVersionStatus.Archived, "error.validation.reference", 404);
        Require(!await Context.PriceListVersions.AnyAsync(v => v.PriceListId == priceListId && v.VersionCode == code, ct),
            "error.sd.pricelist_duplicate_version", 409);

        var clone = new PriceListVersion
        {
            PriceListId = priceListId,
            VersionCode = code,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Status = PriceListVersionStatus.Draft,
            Items = source!.Items.Select(i => new PriceListItem
            {
                ItemId = i.ItemId,
                UomId = i.UomId,
                UnitPrice = i.UnitPrice,
                MinQuantity = i.MinQuantity,
                DiscountPercent = i.DiscountPercent
            }).ToList()
        };
        Context.PriceListVersions.Add(clone);
        await Context.SaveChangesAsync(ct);
        Context.ChangeTracker.Clear();
        var created = await Context.PriceListVersions
            .Include(v => v.Items).ThenInclude(i => i.Item).ThenInclude(i => i!.BaseUom)
            .SingleAsync(v => v.Id == clone.Id, ct);
        return MapVersion(created);
    }

    public async Task<BulkPriceAdjustmentResponse> BulkAdjustVersionAsync(int priceListId, int versionId, BulkPriceAdjustmentRequest request, CancellationToken ct = default)
    {
        Require(Enum.IsDefined(request.AdjustmentType)
            && request.Value is >= -100000m and <= 100000m
            && request.RoundingIncrement is > 0m and <= 1000000m,
            "error.validation.request");
        var version = await Context.PriceListVersions
            .Include(v => v.Items)
            .SingleOrDefaultAsync(v => v.Id == versionId && v.PriceListId == priceListId, ct);
        Require(version != null, "error.validation.reference", 404);
        Require(version!.Status == PriceListVersionStatus.Draft, "error.sd.pricelist_version_not_draft", 409);

        foreach (var line in version.Items)
        {
            var adjusted = request.AdjustmentType switch
            {
                PriceAdjustmentType.Percentage => line.UnitPrice * (1m + request.Value / 100m),
                PriceAdjustmentType.FixedAmount => line.UnitPrice + request.Value,
                _ => throw new InvalidOperationException("Unsupported price adjustment type.")
            };
            Require(adjusted >= 0m, "error.validation.number");
            line.UnitPrice = decimal.Round(
                decimal.Round(adjusted / request.RoundingIncrement, 0, MidpointRounding.AwayFromZero) * request.RoundingIncrement,
                4,
                MidpointRounding.AwayFromZero);
        }

        await Context.SaveChangesAsync(ct);
        return new BulkPriceAdjustmentResponse
        {
            PriceListVersionId = version.Id,
            AdjustedLineCount = version.Items.Count,
            RoundingIncrement = request.RoundingIncrement
        };
    }

    private static void ApplyHeader(CreatePriceListRequest request, PriceList entity)
    {
        entity.Code = request.Code.Trim().ToUpperInvariant();
        entity.Name = request.Name.Trim();
        entity.Currency = request.Currency.Trim().ToUpperInvariant();
        entity.Type = request.Type;
        entity.IsTaxInclusive = request.IsTaxInclusive;
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
        Require(request.Currency?.Trim().Length == 3 && Enum.IsDefined(request.Type), "error.validation.request");
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
