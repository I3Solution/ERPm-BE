using ERPm.Application.Common;
using ERPm.Application.Common.Crud;
using ERPm.Application.Common.Interfaces;
using ERPm.Application.Common.Notifications;
using ERPm.Application.DTOs.Sales;
using ERPm.Application.Features.System;
using ERPm.Domain.Entities.Partner;
using ERPm.Domain.Entities.Sales;
using ERPm.Domain.Enums;
using ERPm.Domain.Services.Iam;
using ERPm.Domain.Services.Sales;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;
using static ERPm.Infrastructure.Services.Common.PostingTransaction;

namespace ERPm.Infrastructure.Services;

public sealed class PriceListService(
    ApplicationDbContext context,
    PostingTransaction? posting = null,
    ICurrentUser? currentUser = null,
    IIamService? iam = null,
    INotificationEngine? notificationEngine = null)
    : BaseCrudService<PriceList, PriceListDetailDto, CreatePriceListRequest, UpdatePriceListRequest>(context), IPricingService
{
    private const int MaxLines = 1000;
    private const int MaxReasonLength = 500;

    private PostingTransaction Tx => posting ?? new PostingTransaction(Context);
    private bool IsBypassUser => currentUser == null || currentUser.IsSystem || currentUser.IsAdmin;

    protected override IQueryable<PriceList> PrepareQuery(IQueryable<PriceList> query) => query.Include(p => p.Branch);

    protected override IQueryable<PriceList> ApplyFilters(IQueryable<PriceList> query, BaseFilterQuery filter)
    {
        query = base.ApplyFilters(query, filter);
        if (filter is PriceListFilterQuery { Type: { } type })
        {
            Require(Enum.IsDefined(type), "error.validation.request");
            query = query.Where(p => p.Type == type);
        }
        return query;
    }

    public override async Task<PagedResult<PriceListDetailDto>> GetPagedAsync(BaseFilterQuery query, CancellationToken cancellationToken = default)
    {
        if (IsBypassUser) return await base.GetPagedAsync(query, cancellationToken);

        var principal = await ResolvePrincipalAsync(cancellationToken);
        var sales = principal.Evaluate(ActionFor(PriceListType.Sales, PermissionCatalog.View));
        var purchase = principal.Evaluate(ActionFor(PriceListType.Purchase, PermissionCatalog.View));

        if (query is PriceListFilterQuery { Type: { } requestedType })
        {
            Require(Enum.IsDefined(requestedType), "error.validation.request");
            var allowed = requestedType == PriceListType.Purchase ? purchase.Allowed : sales.Allowed;
            if (!allowed) throw new BusinessException("error.auth.forbidden", 403);
        }
        else if (!sales.Allowed && !purchase.Allowed)
        {
            throw new BusinessException("error.auth.forbidden", 403);
        }

        var salesAllowed = sales.Allowed;
        var salesUnrestricted = sales.Scope.Unrestricted;
        var salesBranches = sales.Scope.BranchIds.ToArray();
        var salesOwn = sales.Scope.OwnRecords;

        var purchaseAllowed = purchase.Allowed;
        var purchaseUnrestricted = purchase.Scope.Unrestricted;
        var purchaseBranches = purchase.Scope.BranchIds.ToArray();
        var purchaseOwn = purchase.Scope.OwnRecords;

        var userKey = principal.Context.UserKey;

        var queryable = (query.Summary ? PrepareSummaryQuery(DbSet.AsNoTracking()) : PrepareQuery(DbSet.AsNoTracking()))
            .Where(p => !p.IsDeleted)
            .Where(p =>
                (p.Type == PriceListType.Sales && salesAllowed &&
                    (salesUnrestricted || p.BranchId == null || salesBranches.Contains(p.BranchId.Value) || (salesOwn && userKey != null && p.CreatedBy == userKey)))
                || (p.Type == PriceListType.Purchase && purchaseAllowed &&
                    (purchaseUnrestricted || p.BranchId == null || purchaseBranches.Contains(p.BranchId.Value) || (purchaseOwn && userKey != null && p.CreatedBy == userKey))));

        queryable = ApplyFilters(queryable, query);
        var totalCount = await queryable.CountAsync(cancellationToken);
        queryable = ApplySorting(queryable, query);

        var pageIndex = Math.Clamp(query.PageIndex, 1, 1000000);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var items = await queryable.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<PriceListDetailDto>
        {
            Items = items.Select(MapToDto).ToList(),
            TotalCount = totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    public override async Task<PriceListDetailDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await FindEntityAsync(id, cancellationToken);
        if (entity == null) return null;
        await AuthorizeAsync(entity.Type, [PermissionCatalog.View], entity.BranchId, isRead: true, entity.CreatedBy, cancellationToken);
        return MapToDto(entity);
    }

    protected override Task<PriceList?> FindEntityAsync(int id, CancellationToken ct) => Context.PriceLists
        .Include(p => p.Branch)
        .Include(p => p.Versions).ThenInclude(v => v.Items).ThenInclude(i => i.Uom)
        .Include(p => p.Versions).ThenInclude(v => v.Items).ThenInclude(i => i.Item).ThenInclude(i => i!.BaseUom)
        .SingleOrDefaultAsync(p => p.Id == id, ct);

    private static PriceListVersion? CurrentVersion(PriceList list) => list.Versions
        .Where(v => v.Status == PriceListVersionStatus.Active)
        .OrderByDescending(v => v.EffectiveFrom).ThenByDescending(v => v.Id)
        .FirstOrDefault();

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
        UomId = item.UomId,
        ItemCode = item.Item?.Code ?? string.Empty,
        ItemName = item.Item?.Name ?? string.Empty,
        UnitName = item.Uom?.Name ?? item.Item?.BaseUom?.Name,
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
        SubmittedBy = version.SubmittedBy,
        SubmittedAtUtc = version.SubmittedAtUtc,
        ApprovedBy = version.ApprovedBy,
        ApprovedAtUtc = version.ApprovedAtUtc,
        RejectedBy = version.RejectedBy,
        RejectedAtUtc = version.RejectedAtUtc,
        RejectReason = version.RejectReason,
        ActivatedBy = version.ActivatedBy,
        ActivatedAtUtc = version.ActivatedAtUtc,
        ClosedBy = version.ClosedBy,
        ClosedAtUtc = version.ClosedAtUtc,
        CloseReason = version.CloseReason,
        ArchivedBy = version.ArchivedBy,
        ArchivedAtUtc = version.ArchivedAtUtc,
        Items = version.Items.OrderBy(i => i.Item?.Code).ThenBy(i => i.MinQuantity).Select(MapLine).ToList()
    };

    private static PriceListVersionAuditDto MapAudit(PriceListVersionAudit audit) => new()
    {
        Id = audit.Id,
        PriceListVersionId = audit.PriceListVersionId,
        Action = audit.Action,
        FromStatus = audit.FromStatus,
        ToStatus = audit.ToStatus,
        ActorUserId = audit.ActorUserId,
        ActorName = audit.ActorName,
        Reason = audit.Reason,
        OccurredAtUtc = audit.OccurredAtUtc
    };

    // New price lists always initialize V1 in Draft so prices must go through Submit -> Approve -> Activate.
    protected override PriceList MapToEntity(CreatePriceListRequest request) => new()
    {
        Versions =
        {
            new PriceListVersion
            {
                VersionCode = "V1",
                Status = PriceListVersionStatus.Draft,
                Items = (request.Items ?? []).Select(l => new PriceListItem
                {
                    ItemId = l.ItemId,
                    UnitPrice = l.UnitPrice,
                    MinQuantity = l.MinQuantity,
                    DiscountPercent = l.DiscountPercent
                }).ToList()
            }
        }
    };

    protected override async Task BeforeCreateAsync(PriceList entity, CreatePriceListRequest request, CancellationToken ct)
    {
        await ValidateHeaderAsync(request, null, ct);
        await ValidateLinesAsync(request.Items ?? [], ct);
        await AuthorizeAsync(request.Type, [PermissionCatalog.Create], request.BranchId, isRead: false, currentUser?.UserId, ct);
        ApplyHeader(request, entity);
        var initial = entity.Versions.FirstOrDefault();
        if (initial != null)
        {
            initial.EffectiveFrom = entity.EffectiveFrom;
            initial.EffectiveTo = entity.EffectiveTo;
        }
    }

    // Header updates never mutate active version lines or dates; price edits must go through a Draft version.
    protected override async Task BeforeUpdateAsync(PriceList entity, UpdatePriceListRequest request, CancellationToken ct)
    {
        await AuthorizeAsync(entity.Type, [PermissionCatalog.Edit], entity.BranchId, isRead: false, entity.CreatedBy, ct);
        await ValidateHeaderAsync(request, entity.Id, ct);
        if (request.Type != entity.Type || request.BranchId != entity.BranchId)
            await AuthorizeAsync(request.Type, [PermissionCatalog.Edit], request.BranchId, isRead: false, entity.CreatedBy, ct);
    }

    protected override void MapUpdateToEntity(UpdatePriceListRequest request, PriceList entity)
    {
        ApplyHeader(request, entity);
    }

    protected override async Task BeforeDeleteAsync(PriceList entity, CancellationToken ct)
    {
        await AuthorizeAsync(entity.Type, [PermissionCatalog.Delete], entity.BranchId, isRead: false, entity.CreatedBy, ct);
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
        var priceList = await LoadPriceListOrNotFoundAsync(priceListId, ct);
        await AuthorizeAsync(priceList.Type, [PermissionCatalog.View], priceList.BranchId, isRead: true, priceList.CreatedBy, ct);

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
                ItemCount = v.Items.Count,
                SubmittedBy = v.SubmittedBy,
                SubmittedAtUtc = v.SubmittedAtUtc,
                ApprovedBy = v.ApprovedBy,
                ApprovedAtUtc = v.ApprovedAtUtc,
                RejectedBy = v.RejectedBy,
                RejectedAtUtc = v.RejectedAtUtc,
                RejectReason = v.RejectReason,
                ActivatedBy = v.ActivatedBy,
                ActivatedAtUtc = v.ActivatedAtUtc,
                ClosedBy = v.ClosedBy,
                ClosedAtUtc = v.ClosedAtUtc,
                CloseReason = v.CloseReason,
                ArchivedBy = v.ArchivedBy,
                ArchivedAtUtc = v.ArchivedAtUtc
            })
            .ToListAsync(ct);
    }

    public async Task<PriceListVersionDto> GetVersionAsync(int priceListId, int versionId, CancellationToken ct = default)
    {
        var version = await Context.PriceListVersions
            .AsNoTracking()
            .Include(v => v.PriceList)
            .Include(v => v.Items).ThenInclude(i => i.Uom)
            .Include(v => v.Items).ThenInclude(i => i.Item).ThenInclude(i => i!.BaseUom)
            .SingleOrDefaultAsync(v => v.Id == versionId && v.PriceListId == priceListId, ct);
        Require(version != null && version.PriceList != null, "error.validation.reference", 404);
        await AuthorizeAsync(version!.PriceList!.Type, [PermissionCatalog.View], version.PriceList.BranchId, isRead: true, version.PriceList.CreatedBy, ct);
        return MapVersion(version);
    }

    public async Task<List<PriceListVersionAuditDto>> GetVersionAuditAsync(int priceListId, int versionId, CancellationToken ct = default)
    {
        var version = await Context.PriceListVersions
            .AsNoTracking()
            .Include(v => v.PriceList)
            .SingleOrDefaultAsync(v => v.Id == versionId && v.PriceListId == priceListId, ct);
        Require(version != null && version.PriceList != null, "error.validation.reference", 404);
        await AuthorizeAsync(version!.PriceList!.Type, [PermissionCatalog.View], version.PriceList.BranchId, isRead: true, version.PriceList.CreatedBy, ct);

        var logs = await Context.PriceListVersionAudits
            .AsNoTracking()
            .Where(a => a.PriceListVersionId == versionId)
            .OrderByDescending(a => a.OccurredAtUtc)
            .ThenByDescending(a => a.Id)
            .ToListAsync(ct);
        return logs.Select(MapAudit).ToList();
    }

    public async Task<PriceListVersionDto> CreateVersionAsync(int priceListId, CreatePriceListVersionRequest request, CancellationToken ct = default)
    {
        var code = NormalizeAndValidateVersionMetadata(request.VersionCode, request.EffectiveFrom, request.EffectiveTo);
        var lines = request.Items ?? [];
        await ValidateLinesAsync(lines, ct);

        var createdId = await RunInLockAsync(priceListId, async () =>
        {
            var priceList = await LoadPriceListOrNotFoundAsync(priceListId, ct);
            await AuthorizeAsync(priceList.Type, [PermissionCatalog.Edit], priceList.BranchId, isRead: false, priceList.CreatedBy, ct);
            await EnsureVersionCodeUniqueAsync(priceListId, code, null, ct);

            var version = new PriceListVersion
            {
                PriceListId = priceListId,
                VersionCode = code,
                EffectiveFrom = request.EffectiveFrom,
                EffectiveTo = request.EffectiveTo,
                Status = PriceListVersionStatus.Draft,
                Items = lines.Select(l => new PriceListItem
                {
                    ItemId = l.ItemId,
                    UnitPrice = l.UnitPrice,
                    MinQuantity = l.MinQuantity,
                    DiscountPercent = l.DiscountPercent
                }).ToList()
            };

            Context.PriceListVersions.Add(version);
            await Context.SaveChangesAsync(ct);
            return version.Id;
        }, ct);

        Context.ChangeTracker.Clear();
        return await LoadVersionDtoNoAuthAsync(priceListId, createdId, ct);
    }

    public async Task<PriceListVersionDto> UpdateVersionAsync(int priceListId, int versionId, UpdatePriceListVersionRequest request, CancellationToken ct = default)
    {
        var code = NormalizeAndValidateVersionMetadata(request.VersionCode, request.EffectiveFrom, request.EffectiveTo);

        await RunInLockAsync(priceListId, async () =>
        {
            var version = await FindVersionForDraftEditAsync(priceListId, versionId, ct);
            await EnsureVersionCodeUniqueAsync(priceListId, code, versionId, ct);

            version.VersionCode = code;
            version.EffectiveFrom = request.EffectiveFrom;
            version.EffectiveTo = request.EffectiveTo;
            version.LastModifiedAtUtc = DateTime.UtcNow;
            return true;
        }, ct);

        Context.ChangeTracker.Clear();
        return await LoadVersionDtoNoAuthAsync(priceListId, versionId, ct);
    }

    public async Task DeleteVersionAsync(int priceListId, int versionId, CancellationToken ct = default)
    {
        await RunInLockAsync(priceListId, async () =>
        {
            var version = await Context.PriceListVersions
                .Include(v => v.PriceList)
                .SingleOrDefaultAsync(v => v.Id == versionId && v.PriceListId == priceListId, ct);
            Require(version != null && version.PriceList != null, "error.validation.reference", 404);
            await AuthorizeAsync(version!.PriceList!.Type, [PermissionCatalog.Delete], version.PriceList.BranchId, isRead: false, version.PriceList.CreatedBy, ct);
            Require(version.Status == PriceListVersionStatus.Draft, "error.sd.pricelist_version_not_draft", 409);

            version.IsDeleted = true;
            version.DeletedAtUtc = DateTime.UtcNow;
            version.DeletedBy = currentUser?.UserId;
            return true;
        }, ct);
    }

    public async Task<PriceListVersionDto> SubmitVersionAsync(int priceListId, int versionId, PriceListVersionTransitionRequest? request = null, CancellationToken ct = default)
    {
        var reason = NormalizeOptionalReason(request?.Reason);
        DocumentNotice? notice = null;

        await RunInLockAsync(priceListId, async () =>
        {
            var version = await LoadVersionWithPriceListAndItemsAsync(priceListId, versionId, ct);
            await AuthorizeAsync(version.PriceList!.Type, [PermissionCatalog.Submit], version.PriceList.BranchId, isRead: false, version.PriceList.CreatedBy, ct);
            Require(version.Status == PriceListVersionStatus.Draft, "error.sd.pricelist_version_invalid_status", 409);
            Require(version.Items.Count > 0, "error.sd.pricelist_version_empty", 400);

            var now = DateTime.UtcNow;
            var (actorId, actorName) = await ResolveActorAsync(ct);
            var fromStatus = version.Status;

            version.Status = PriceListVersionStatus.PendingApproval;
            version.SubmittedBy = actorId;
            version.SubmittedAtUtc = now;
            version.RejectReason = null;
            version.LastModifiedAtUtc = now;

            RecordAudit(version, "SUBMIT", fromStatus, version.Status, actorId, actorName, reason, now);
            notice = BuildNotice("SD_PRICELIST_SUBMITTED", NotificationSeverity.INFO, version.PriceList, version,
                ActionFor(version.PriceList.Type, PermissionCatalog.Approve), reason, globalReachesAllAllowed: false, $"SD_PL_SUBMIT_{version.Id}_{now.Ticks}");
            return true;
        }, ct);

        await PublishAsync(notice, ct);
        Context.ChangeTracker.Clear();
        return await LoadVersionDtoNoAuthAsync(priceListId, versionId, ct);
    }

    public async Task<PriceListVersionDto> WithdrawVersionAsync(int priceListId, int versionId, PriceListVersionTransitionRequest? request = null, CancellationToken ct = default)
    {
        var reason = NormalizeOptionalReason(request?.Reason);

        await RunInLockAsync(priceListId, async () =>
        {
            var version = await LoadVersionWithPriceListAsync(priceListId, versionId, ct);
            await AuthorizeAsync(version.PriceList!.Type, [PermissionCatalog.Withdraw], version.PriceList.BranchId, isRead: false, version.PriceList.CreatedBy, ct);
            Require(version.Status is PriceListVersionStatus.PendingApproval or PriceListVersionStatus.Approved,
                "error.sd.pricelist_version_invalid_status", 409);

            var now = DateTime.UtcNow;
            var (actorId, actorName) = await ResolveActorAsync(ct);
            var fromStatus = version.Status;

            version.Status = PriceListVersionStatus.Draft;
            version.LastModifiedAtUtc = now;

            RecordAudit(version, "WITHDRAW", fromStatus, version.Status, actorId, actorName, reason, now);
            return true;
        }, ct);

        Context.ChangeTracker.Clear();
        return await LoadVersionDtoNoAuthAsync(priceListId, versionId, ct);
    }

    public async Task<PriceListVersionDto> ApproveVersionAsync(int priceListId, int versionId, PriceListVersionTransitionRequest? request = null, CancellationToken ct = default)
    {
        var reason = NormalizeOptionalReason(request?.Reason);
        DocumentNotice? notice = null;

        await RunInLockAsync(priceListId, async () =>
        {
            var version = await LoadVersionWithPriceListAsync(priceListId, versionId, ct);
            await AuthorizeAsync(version.PriceList!.Type, [PermissionCatalog.Approve], version.PriceList.BranchId, isRead: false, version.PriceList.CreatedBy, ct);
            Require(version.Status == PriceListVersionStatus.PendingApproval, "error.sd.pricelist_version_invalid_status", 409);

            var now = DateTime.UtcNow;
            var (actorId, actorName) = await ResolveActorAsync(ct);
            var fromStatus = version.Status;

            version.Status = PriceListVersionStatus.Approved;
            version.ApprovedBy = actorId;
            version.ApprovedAtUtc = now;
            version.LastModifiedAtUtc = now;

            RecordAudit(version, "APPROVE", fromStatus, version.Status, actorId, actorName, reason, now);
            notice = BuildNotice("SD_PRICELIST_APPROVED", NotificationSeverity.SUCCESS, version.PriceList, version,
                ActionFor(version.PriceList.Type, PermissionCatalog.Activate), reason, globalReachesAllAllowed: false, $"SD_PL_APPROVE_{version.Id}_{now.Ticks}");
            return true;
        }, ct);

        await PublishAsync(notice, ct);
        Context.ChangeTracker.Clear();
        return await LoadVersionDtoNoAuthAsync(priceListId, versionId, ct);
    }

    public async Task<PriceListVersionDto> RejectVersionAsync(int priceListId, int versionId, PriceListVersionTransitionRequest request, CancellationToken ct = default)
    {
        var reason = NormalizeRequiredReason(request?.Reason);
        DocumentNotice? notice = null;

        await RunInLockAsync(priceListId, async () =>
        {
            var version = await LoadVersionWithPriceListAsync(priceListId, versionId, ct);
            await AuthorizeAsync(version.PriceList!.Type, [PermissionCatalog.Reject], version.PriceList.BranchId, isRead: false, version.PriceList.CreatedBy, ct);
            Require(version.Status == PriceListVersionStatus.PendingApproval, "error.sd.pricelist_version_invalid_status", 409);

            var now = DateTime.UtcNow;
            var (actorId, actorName) = await ResolveActorAsync(ct);
            var fromStatus = version.Status;

            version.Status = PriceListVersionStatus.Draft;
            version.RejectedBy = actorId;
            version.RejectedAtUtc = now;
            version.RejectReason = reason;
            version.LastModifiedAtUtc = now;

            RecordAudit(version, "REJECT", fromStatus, version.Status, actorId, actorName, reason, now);
            notice = BuildNotice("SD_PRICELIST_REJECTED", NotificationSeverity.WARNING, version.PriceList, version,
                ActionFor(version.PriceList.Type, PermissionCatalog.Edit), reason, globalReachesAllAllowed: false, $"SD_PL_REJECT_{version.Id}_{now.Ticks}",
                ownerOverride: version.SubmittedBy ?? version.CreatedBy);
            return true;
        }, ct);

        await PublishAsync(notice, ct);
        Context.ChangeTracker.Clear();
        return await LoadVersionDtoNoAuthAsync(priceListId, versionId, ct);
    }

    public async Task<PriceListVersionDto> ActivateVersionAsync(int priceListId, int versionId, PriceListVersionTransitionRequest? request = null, CancellationToken ct = default)
    {
        var reason = NormalizeOptionalReason(request?.Reason);
        DocumentNotice? notice = null;

        await RunInLockAsync(priceListId, async () =>
        {
            var version = await LoadVersionWithPriceListAndItemsAsync(priceListId, versionId, ct);
            var priceList = version.PriceList!;
            await AuthorizeAsync(priceList.Type, [PermissionCatalog.Activate], priceList.BranchId, isRead: false, priceList.CreatedBy, ct);
            Require(version.Status == PriceListVersionStatus.Approved, "error.sd.pricelist_version_invalid_status", 409);
            Require(priceList.IsActive, "error.sd.pricelist_inactive", 409);
            if (priceList.BranchId.HasValue)
            {
                var branchActive = await Context.Branches.IgnoreQueryFilters()
                    .AnyAsync(b => !b.IsDeleted && b.Id == priceList.BranchId.Value && b.IsActive, ct);
                Require(branchActive, "error.validation.reference");
            }
            Require(version.Items.Count > 0, "error.sd.pricelist_version_empty", 400);

            var now = DateTime.UtcNow;
            // Reject activation when the version or its parent price list has already passed EffectiveTo.
            if ((version.EffectiveTo.HasValue && version.EffectiveTo.Value < now)
                || (priceList.EffectiveTo.HasValue && priceList.EffectiveTo.Value < now))
                throw new BusinessException("error.sd.pricelist_version_expired", 409);

            // Reject activation when the version window extends outside the parent price list effective range.
            if (version.EffectiveFrom < priceList.EffectiveFrom
                || (priceList.EffectiveTo.HasValue && (!version.EffectiveTo.HasValue || version.EffectiveTo.Value > priceList.EffectiveTo.Value)))
                throw new BusinessException("error.sd.pricelist_version_outside_header_range", 409);

            // Effective windows are inclusive (pricing uses <= / >=), so windows touching on one day also overlap.
            var from = version.EffectiveFrom;
            var to = version.EffectiveTo;
            var overlapping = await Context.PriceListVersions
                .Where(v => v.PriceListId == priceListId && v.Id != versionId && v.Status == PriceListVersionStatus.Active
                    && (to == null || v.EffectiveFrom <= to) && (v.EffectiveTo == null || v.EffectiveTo >= from))
                .OrderBy(v => v.EffectiveFrom).FirstOrDefaultAsync(ct);
            if (overlapping != null)
                throw new BusinessException("error.sd.pricelist_version_overlap", 409, new Dictionary<string, string>
                {
                    ["code"] = overlapping.VersionCode,
                    ["from"] = overlapping.EffectiveFrom.ToString("dd/MM/yyyy"),
                    ["to"] = overlapping.EffectiveTo?.ToString("dd/MM/yyyy") ?? "\u221e"
                });

            var (actorId, actorName) = await ResolveActorAsync(ct);
            var fromStatus = version.Status;

            version.Status = PriceListVersionStatus.Active;
            version.ActivatedBy = actorId;
            version.ActivatedAtUtc = now;
            version.LastModifiedAtUtc = now;

            RecordAudit(version, "ACTIVATE", fromStatus, version.Status, actorId, actorName, reason, now);
            notice = BuildNotice("SD_PRICELIST_ACTIVATED", NotificationSeverity.SUCCESS, priceList, version,
                ActionFor(priceList.Type, PermissionCatalog.View), reason, globalReachesAllAllowed: true, $"SD_PL_ACTIVATE_{version.Id}_{now.Ticks}");
            return true;
        }, ct);

        await PublishAsync(notice, ct);
        Context.ChangeTracker.Clear();
        return await LoadVersionDtoNoAuthAsync(priceListId, versionId, ct);
    }

    public async Task<PriceListVersionDto> CloseVersionAsync(int priceListId, int versionId, PriceListVersionTransitionRequest request, CancellationToken ct = default)
    {
        var reason = NormalizeRequiredReason(request?.Reason);
        DocumentNotice? notice = null;

        await RunInLockAsync(priceListId, async () =>
        {
            var version = await LoadVersionWithPriceListAsync(priceListId, versionId, ct);
            await AuthorizeAsync(version.PriceList!.Type, [PermissionCatalog.Close], version.PriceList.BranchId, isRead: false, version.PriceList.CreatedBy, ct);
            Require(version.Status == PriceListVersionStatus.Active, "error.sd.pricelist_version_invalid_status", 409);

            var now = DateTime.UtcNow;
            var (actorId, actorName) = await ResolveActorAsync(ct);
            var fromStatus = version.Status;

            // Keep original EffectiveTo intact so scheduled future versions do not violate effective_to >= effective_from.
            version.Status = PriceListVersionStatus.Expired;
            version.ClosedBy = actorId;
            version.ClosedAtUtc = now;
            version.CloseReason = reason;
            version.LastModifiedAtUtc = now;

            RecordAudit(version, "CLOSE", fromStatus, version.Status, actorId, actorName, reason, now);
            notice = BuildNotice("SD_PRICELIST_CLOSED", NotificationSeverity.WARNING, version.PriceList, version,
                ActionFor(version.PriceList.Type, PermissionCatalog.View), reason, globalReachesAllAllowed: true, $"SD_PL_CLOSE_{version.Id}_{now.Ticks}");
            return true;
        }, ct);

        await PublishAsync(notice, ct);
        Context.ChangeTracker.Clear();
        return await LoadVersionDtoNoAuthAsync(priceListId, versionId, ct);
    }

    public async Task<PriceListVersionDto> ArchiveVersionAsync(int priceListId, int versionId, PriceListVersionTransitionRequest? request = null, CancellationToken ct = default)
    {
        var reason = NormalizeOptionalReason(request?.Reason);

        await RunInLockAsync(priceListId, async () =>
        {
            var version = await LoadVersionWithPriceListAsync(priceListId, versionId, ct);
            await AuthorizeAsync(version.PriceList!.Type, [PermissionCatalog.Archive], version.PriceList.BranchId, isRead: false, version.PriceList.CreatedBy, ct);
            Require(version.Status == PriceListVersionStatus.Expired, "error.sd.pricelist_version_invalid_status", 409);

            var now = DateTime.UtcNow;
            var (actorId, actorName) = await ResolveActorAsync(ct);
            var fromStatus = version.Status;

            version.Status = PriceListVersionStatus.Archived;
            version.ArchivedBy = actorId;
            version.ArchivedAtUtc = now;
            version.LastModifiedAtUtc = now;

            RecordAudit(version, "ARCHIVE", fromStatus, version.Status, actorId, actorName, reason, now);
            return true;
        }, ct);

        Context.ChangeTracker.Clear();
        return await LoadVersionDtoNoAuthAsync(priceListId, versionId, ct);
    }

    public async Task<PriceListItemDto> AddVersionItemAsync(int priceListId, int versionId, CreatePriceListItemRequest request, CancellationToken ct = default)
    {
        ValidateLineValues(request.UnitPrice, request.MinQuantity, request.DiscountPercent);

        var lineId = await RunInLockAsync(priceListId, async () =>
        {
            var version = await FindVersionForDraftEditAsync(priceListId, versionId, ct);
            Require(await Context.PriceListItems.CountAsync(i => i.PriceListVersionId == versionId, ct) < MaxLines, "error.validation.request");
            await EnsureItemAndUomValidAsync(request.ItemId, request.UomId, ct);
            await EnsureNoDuplicateVersionLineAsync(versionId, request.ItemId, request.UomId, request.MinQuantity, null, ct);

            var line = new PriceListItem
            {
                PriceListVersionId = versionId,
                ItemId = request.ItemId,
                UomId = request.UomId,
                UnitPrice = request.UnitPrice,
                MinQuantity = request.MinQuantity,
                DiscountPercent = request.DiscountPercent
            };

            Context.PriceListItems.Add(line);
            version.LastModifiedAtUtc = DateTime.UtcNow;
            await Context.SaveChangesAsync(ct);
            return line.Id;
        }, ct);

        Context.ChangeTracker.Clear();
        return await LoadVersionItemDtoAsync(lineId, ct);
    }

    public async Task<PriceListItemDto> UpdateVersionItemAsync(int priceListId, int versionId, int itemId, UpdatePriceListItemRequest request, CancellationToken ct = default)
    {
        ValidateLineValues(request.UnitPrice, request.MinQuantity, request.DiscountPercent);

        await RunInLockAsync(priceListId, async () =>
        {
            var version = await FindVersionForDraftEditAsync(priceListId, versionId, ct);
            var line = await Context.PriceListItems.SingleOrDefaultAsync(i => i.Id == itemId && i.PriceListVersionId == versionId, ct);
            Require(line != null, "error.validation.reference", 404);

            var targetItemId = request.ItemId ?? line!.ItemId;
            await EnsureItemAndUomValidAsync(targetItemId, request.UomId, ct);
            await EnsureNoDuplicateVersionLineAsync(versionId, targetItemId, request.UomId, request.MinQuantity, line!.Id, ct);

            line.ItemId = targetItemId;
            line.UomId = request.UomId;
            line.UnitPrice = request.UnitPrice;
            line.MinQuantity = request.MinQuantity;
            line.DiscountPercent = request.DiscountPercent;
            version.LastModifiedAtUtc = DateTime.UtcNow;
            return true;
        }, ct);

        Context.ChangeTracker.Clear();
        return await LoadVersionItemDtoAsync(itemId, ct);
    }

    public async Task DeleteVersionItemAsync(int priceListId, int versionId, int itemId, CancellationToken ct = default)
    {
        await RunInLockAsync(priceListId, async () =>
        {
            var version = await FindVersionForDraftEditAsync(priceListId, versionId, ct);
            var line = await Context.PriceListItems.SingleOrDefaultAsync(i => i.Id == itemId && i.PriceListVersionId == versionId, ct);
            Require(line != null, "error.validation.reference", 404);

            Context.PriceListItems.Remove(line!);
            version.LastModifiedAtUtc = DateTime.UtcNow;
            return true;
        }, ct);
    }

    public async Task<PriceListVersionDto> CloneVersionAsync(int priceListId, ClonePriceListVersionRequest request, CancellationToken ct = default)
    {
        var code = NormalizeAndValidateVersionMetadata(request.VersionCode, request.EffectiveFrom, request.EffectiveTo);

        var cloneId = await RunInLockAsync(priceListId, async () =>
        {
            var source = await Context.PriceListVersions
                .Include(v => v.PriceList)
                .Include(v => v.Items)
                .SingleOrDefaultAsync(v => v.Id == request.SourceVersionId && v.PriceListId == priceListId, ct);
            Require(source != null && source.PriceList != null && source.Status != PriceListVersionStatus.Archived, "error.validation.reference", 404);
            await AuthorizeAsync(source!.PriceList!.Type, [PermissionCatalog.Create, PermissionCatalog.Edit], source.PriceList.BranchId, isRead: false, source.PriceList.CreatedBy, ct);
            await EnsureVersionCodeUniqueAsync(priceListId, code, null, ct);

            var clone = new PriceListVersion
            {
                PriceListId = priceListId,
                VersionCode = code,
                EffectiveFrom = request.EffectiveFrom,
                EffectiveTo = request.EffectiveTo,
                Status = PriceListVersionStatus.Draft,
                Items = source.Items.Select(i => new PriceListItem
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
            return clone.Id;
        }, ct);

        Context.ChangeTracker.Clear();
        return await LoadVersionDtoNoAuthAsync(priceListId, cloneId, ct);
    }

    public async Task<BulkPriceAdjustmentResponse> BulkAdjustVersionAsync(int priceListId, int versionId, BulkPriceAdjustmentRequest request, CancellationToken ct = default)
    {
        Require(Enum.IsDefined(request.AdjustmentType)
            && request.Value is >= -100000m and <= 100000m
            && request.RoundingIncrement is > 0m and <= 1000000m,
            "error.validation.request");

        return await RunInLockAsync(priceListId, async () =>
        {
            var version = await LoadVersionWithPriceListAndItemsAsync(priceListId, versionId, ct);
            await AuthorizeAsync(version.PriceList!.Type, [PermissionCatalog.Edit], version.PriceList.BranchId, isRead: false, version.PriceList.CreatedBy, ct);
            Require(version.Status == PriceListVersionStatus.Draft, "error.sd.pricelist_version_not_draft", 409);

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

            version.LastModifiedAtUtc = DateTime.UtcNow;
            return new BulkPriceAdjustmentResponse
            {
                PriceListVersionId = version.Id,
                AdjustedLineCount = version.Items.Count,
                RoundingIncrement = request.RoundingIncrement
            };
        }, ct);
    }

    private async Task<T> RunInLockAsync<T>(int priceListId, Func<Task<T>> action, CancellationToken ct)
    {
        if (!Context.Database.IsRelational())
        {
            var result = await action();
            await Context.SaveChangesAsync(ct);
            return result;
        }
        return await Tx.RunAsync(async () =>
        {
            if (Context.Database.IsNpgsql())
                await Tx.LockAsync($"pricelist:{priceListId}", ct);
            return await action();
        }, ct);
    }

    private static string ActionFor(PriceListType type, string verb) =>
        (type == PriceListType.Purchase ? "pur:pricelist:" : "sd:pricelist:") + verb;

    private async Task<IamPrincipal> ResolvePrincipalAsync(CancellationToken ct)
    {
        if (iam == null || !int.TryParse(currentUser?.UserId, out var userId))
            throw new BusinessException("error.auth.forbidden", 403);
        return await iam.GetPrincipalAsync(userId, ct);
    }

    private async Task AuthorizeAsync(PriceListType type, string[] verbs, int? branchId, bool isRead, string? createdBy, CancellationToken ct)
    {
        if (IsBypassUser) return;

        var principal = await ResolvePrincipalAsync(ct);
        var allowed = false;
        var scope = EffectiveDataScope.None;
        foreach (var verb in verbs)
        {
            var decision = principal.Evaluate(ActionFor(type, verb));
            if (!decision.Allowed) continue;
            allowed = true;
            scope = scope.Union(decision.Scope);
        }

        if (!allowed) throw new BusinessException("error.auth.forbidden", 403);
        if (scope.Unrestricted) return;

        var ownsRecord = scope.OwnRecords && createdBy != null && createdBy == principal.Context.UserKey;
        if (isRead)
        {
            var reachesRead = branchId == null || scope.BranchIds.Contains(branchId.Value) || ownsRecord;
            if (!reachesRead) throw new BusinessException("error.auth.forbidden", 403);
            return;
        }

        // System-wide price lists (BranchId == null) require unrestricted (Group) scope for write/lifecycle actions.
        if (branchId == null) throw new BusinessException("error.auth.forbidden", 403);
        var reachesWrite = scope.BranchIds.Contains(branchId.Value) || ownsRecord;
        if (!reachesWrite) throw new BusinessException("error.auth.forbidden", 403);
    }

    private async Task<(string? UserId, string? DisplayName)> ResolveActorAsync(CancellationToken ct)
    {
        var userId = currentUser?.UserId;
        if (string.IsNullOrWhiteSpace(userId) || !int.TryParse(userId, out var parsedId))
            return (userId, null);
        var displayName = await Context.AppUsers.AsNoTracking()
            .Where(u => u.Id == parsedId)
            .Select(u => u.DisplayName)
            .SingleOrDefaultAsync(ct);
        return (userId, displayName);
    }

    private void RecordAudit(
        PriceListVersion version,
        string action,
        PriceListVersionStatus fromStatus,
        PriceListVersionStatus toStatus,
        string? actorId,
        string? actorName,
        string? reason,
        DateTime occurredAtUtc)
    {
        Context.PriceListVersionAudits.Add(new PriceListVersionAudit
        {
            PriceListVersionId = version.Id,
            Action = action,
            FromStatus = fromStatus,
            ToStatus = toStatus,
            ActorUserId = actorId,
            ActorName = actorName,
            Reason = reason,
            OccurredAtUtc = occurredAtUtc
        });
    }

    private static DocumentNotice BuildNotice(
        string template,
        string severity,
        PriceList priceList,
        PriceListVersion version,
        string recipientAction,
        string? reason,
        bool globalReachesAllAllowed,
        string idempotencyPrefix,
        string? ownerOverride = null) => new(
        template,
        priceList.Type == PriceListType.Purchase ? NotificationCategory.PUR : NotificationCategory.SD,
        severity,
        recipientAction,
        priceList.BranchId,
        null,
        ownerOverride ?? version.CreatedBy ?? priceList.CreatedBy,
        new Dictionary<string, object>
        {
            ["pricelist_code"] = priceList.Code,
            ["pricelist_name"] = priceList.Name,
            ["version_code"] = version.VersionCode,
            ["reason"] = reason ?? string.Empty
        },
        $"/sales/price-lists/{priceList.Id}",
        globalReachesAllAllowed,
        idempotencyPrefix);

    private Task PublishAsync(DocumentNotice? notice, CancellationToken ct) =>
        PermissionRecipients.PublishAsync(Context, iam, notificationEngine, currentUser, notice, ct);

    private static string? NormalizeOptionalReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason)) return null;
        var trimmed = reason.Trim();
        Require(trimmed.Length <= MaxReasonLength, "error.validation.request");
        return trimmed;
    }

    private static string NormalizeRequiredReason(string? reason)
    {
        Require(!string.IsNullOrWhiteSpace(reason) && reason.Trim().Length <= MaxReasonLength, "error.validation.request");
        return reason!.Trim();
    }

    private async Task<PriceList> LoadPriceListOrNotFoundAsync(int priceListId, CancellationToken ct)
    {
        var priceList = await Context.PriceLists.SingleOrDefaultAsync(p => p.Id == priceListId, ct);
        Require(priceList != null, "error.validation.reference", 404);
        return priceList!;
    }

    private async Task<PriceListVersion> LoadVersionWithPriceListAsync(int priceListId, int versionId, CancellationToken ct)
    {
        var version = await Context.PriceListVersions
            .Include(v => v.PriceList)
            .SingleOrDefaultAsync(v => v.Id == versionId && v.PriceListId == priceListId, ct);
        Require(version != null && version.PriceList != null, "error.validation.reference", 404);
        return version!;
    }

    private async Task<PriceListVersion> LoadVersionWithPriceListAndItemsAsync(int priceListId, int versionId, CancellationToken ct)
    {
        var version = await Context.PriceListVersions
            .Include(v => v.PriceList)
            .Include(v => v.Items)
            .SingleOrDefaultAsync(v => v.Id == versionId && v.PriceListId == priceListId, ct);
        Require(version != null && version.PriceList != null, "error.validation.reference", 404);
        return version!;
    }

    private async Task<PriceListVersionDto> LoadVersionDtoNoAuthAsync(int priceListId, int versionId, CancellationToken ct)
    {
        var version = await Context.PriceListVersions
            .AsNoTracking()
            .Include(v => v.Items).ThenInclude(i => i.Uom)
            .Include(v => v.Items).ThenInclude(i => i.Item).ThenInclude(i => i!.BaseUom)
            .SingleAsync(v => v.Id == versionId && v.PriceListId == priceListId, ct);
        return MapVersion(version);
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

    private async Task ValidateHeaderAsync(CreatePriceListRequest request, int? selfId, CancellationToken ct)
    {
        var code = request.Code?.Trim() ?? string.Empty;
        Require(code.Length is > 0 and <= 50 && !string.IsNullOrWhiteSpace(request.Name) && request.Name.Trim().Length <= 255, "error.validation.request");
        Require(request.Currency?.Trim().Length == 3 && Enum.IsDefined(request.Type), "error.validation.request");
        Require(request.EffectiveFrom.Kind == DateTimeKind.Utc && (request.EffectiveTo == null || request.EffectiveTo.Value.Kind == DateTimeKind.Utc && request.EffectiveTo >= request.EffectiveFrom), "error.validation.request");
        Require(request.BranchId == null || await Context.Branches.IgnoreQueryFilters().AnyAsync(b => !b.IsDeleted && b.Id == request.BranchId && b.IsActive, ct), "error.validation.reference");
        var normalized = code.ToUpperInvariant();
        Require(!await Context.PriceLists.AnyAsync(p => p.Code == normalized && p.Id != selfId, ct), "error.sd.pricelist_duplicate_code", 409);
    }

    private static string NormalizeAndValidateVersionMetadata(string? versionCode, DateTime effectiveFrom, DateTime? effectiveTo)
    {
        var code = versionCode?.Trim().ToUpperInvariant() ?? string.Empty;
        Require(code.Length is > 0 and <= 50
            && effectiveFrom.Kind == DateTimeKind.Utc
            && (effectiveTo == null || effectiveTo.Value.Kind == DateTimeKind.Utc && effectiveTo >= effectiveFrom),
            "error.validation.request");
        return code;
    }

    private async Task EnsureVersionCodeUniqueAsync(int priceListId, string code, int? excludeVersionId, CancellationToken ct)
    {
        var exists = await Context.PriceListVersions.AnyAsync(
            v => v.PriceListId == priceListId && v.VersionCode == code && (excludeVersionId == null || v.Id != excludeVersionId.Value),
            ct);
        Require(!exists, "error.sd.pricelist_duplicate_version", 409);
    }

    private async Task<PriceListVersion> FindVersionForDraftEditAsync(int priceListId, int versionId, CancellationToken ct)
    {
        var version = await LoadVersionWithPriceListAsync(priceListId, versionId, ct);
        await AuthorizeAsync(version.PriceList!.Type, [PermissionCatalog.Edit], version.PriceList.BranchId, isRead: false, version.PriceList.CreatedBy, ct);
        Require(version.Status == PriceListVersionStatus.Draft, "error.sd.pricelist_version_not_draft", 409);
        return version;
    }

    private static void ValidateLineValues(decimal unitPrice, decimal minQuantity, decimal discountPercent)
    {
        ValidateNumber(unitPrice);
        ValidateNumber(minQuantity);
        ValidateNumber(discountPercent);
        Require(discountPercent <= 100, "error.validation.number");
    }

    private async Task ValidateLinesAsync(IReadOnlyCollection<PriceListLineRequest> lines, CancellationToken ct)
    {
        Require(lines.Count <= MaxLines, "error.validation.request");
        foreach (var line in lines) ValidateLineValues(line.UnitPrice, line.MinQuantity, line.DiscountPercent);
        Require(lines.Select(l => (l.ItemId, l.MinQuantity)).Distinct().Count() == lines.Count, "error.validation.request");
        var itemIds = lines.Select(l => l.ItemId).Distinct().ToArray();
        if (itemIds.Length > 0)
            Require(await Context.Items.CountAsync(i => itemIds.Contains(i.Id) && i.IsActive, ct) == itemIds.Length, "error.inventory.item");
    }

    private async Task EnsureItemAndUomValidAsync(int itemId, int? uomId, CancellationToken ct)
    {
        Require(await Context.Items.AnyAsync(i => i.Id == itemId && i.IsActive, ct), "error.inventory.item");
        if (uomId.HasValue)
            Require(await Context.UnitsOfMeasure.AnyAsync(u => u.Id == uomId.Value && u.IsActive, ct), "error.validation.reference");
    }

    private async Task EnsureNoDuplicateVersionLineAsync(int versionId, int itemId, int? uomId, decimal minQuantity, int? excludeLineId, CancellationToken ct)
    {
        var duplicate = await Context.PriceListItems.AnyAsync(
            i => i.PriceListVersionId == versionId
                && i.ItemId == itemId
                && i.UomId == uomId
                && i.MinQuantity == minQuantity
                && (excludeLineId == null || i.Id != excludeLineId.Value),
            ct);
        Require(!duplicate, "error.sd.pricelist_item_duplicate", 409);
    }

    private async Task<PriceListItemDto> LoadVersionItemDtoAsync(int lineId, CancellationToken ct)
    {
        var line = await Context.PriceListItems
            .AsNoTracking()
            .Include(i => i.Uom)
            .Include(i => i.Item).ThenInclude(i => i!.BaseUom)
            .SingleAsync(i => i.Id == lineId, ct);
        return MapLine(line);
    }
}
