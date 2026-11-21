using ERPm.Infrastructure.Services.Common;
using ERPm.Application.Common.Interfaces;
using ERPm.Application.DTOs.Inventory;
using ERPm.Application.Features.Inventory;
using ERPm.Application.Features.System;
using ERPm.Domain.Entities.Accounting;
using ERPm.Domain.Entities.Inventory;
using ERPm.Domain.Entities.Sales;
using ERPm.Domain.Entities.System;
using ERPm.Application.Common;
using ERPm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ERPm.Application.DTOs.System;

namespace ERPm.Infrastructure.Services;

public class StockService : IStockService
{
    private readonly IApplicationDbContext _context;
    private readonly IBranchService _branchService;
    private readonly StockReceiptPostingService _receipts;
    private readonly ICurrentUser _user;

    public StockService(IApplicationDbContext context, IBranchService branchService, StockReceiptPostingService receipts, ICurrentUser user)
    {
        _context = context;
        _user = user;
        _branchService = branchService;
        _receipts = receipts;
    }

    public async Task<List<WarehouseDto>> GetWarehousesAsync(int? branchId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Warehouses
            .Include(w => w.Branch)
            .Where(w => w.IsActive)
            .AsNoTracking();

        if (branchId.HasValue && branchId.Value > 0)
        {
            var targetBranchIds = await _branchService.GetBranchAndDescendantIdsAsync(branchId.Value, cancellationToken);
            query = query.Where(w => targetBranchIds.Contains(w.BranchId));
        }

        return await query
            .OrderByDescending(w => w.IsDefault)
            .Select(w => new WarehouseDto
            {
                Id = w.Id,
                BranchId = w.BranchId,
                BranchName = w.Branch != null ? w.Branch.Name : null,
                Code = w.Code,
                Name = w.Name,
                Translations = w.Translations,
                Address = w.Address,
                IsDefault = w.IsDefault
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<List<StockBalanceDto>> GetStockBalancesAsync(int? warehouseId, int? branchId = null, ERPm.Application.Common.Crud.ListSort? sort = null, CancellationToken cancellationToken = default)
    {
        var query = _context.StockBalances
            .Include(b => b.Warehouse)
            .Include(b => b.Item)
                .ThenInclude(i => i!.BaseUom)
            .AsNoTracking();

        if (warehouseId.HasValue && warehouseId.Value > 0)
        {
            query = query.Where(b => b.WarehouseId == warehouseId.Value);
        }

        if (branchId.HasValue && branchId.Value > 0)
        {
            var targetBranchIds = await _branchService.GetBranchAndDescendantIdsAsync(branchId.Value, cancellationToken);
            query = query.Where(b => b.Warehouse != null && targetBranchIds.Contains(b.Warehouse.BranchId));
        }

        var balances = query
            .Select(b => new StockBalanceDto
            {
                WarehouseId = b.WarehouseId,
                WarehouseName = b.Warehouse != null ? b.Warehouse.Name : string.Empty,
                ItemId = b.ItemId,
                ItemCode = b.Item != null ? b.Item.Code : string.Empty,
                ItemName = b.Item != null ? b.Item.Name : string.Empty,
                Barcode = b.Item != null ? b.Item.Barcode : null,
                UomName = b.Item != null && b.Item.BaseUom != null ? b.Item.BaseUom.Name : "Cái",
                QuantityOnHand = b.QuantityOnHand,
                QuantityReserved = b.QuantityReserved,
                QuantityAvailable = b.QuantityOnHand - b.QuantityReserved,
                AverageCostPrice = b.AverageCostPrice,
                TotalValue = b.InventoryValue,
                IsLowStock = b.Item != null && b.QuantityOnHand <= b.Item.MinStockAlert
            });
        // Without a requested column the list keeps its item-code order.
        return await (sort != null && DtoSorting.IsRequested(sort) ? balances.OrderByRequested(sort) : balances.OrderBy(b => b.ItemCode)).ToListAsync(cancellationToken);
    }

    public Task<bool> CreateStockReceiptAsync(CreateStockReceiptRequest request, CancellationToken cancellationToken = default)
        => _receipts.CreateAsync(request, cancellationToken);

    public const string PolicyKey = "wms.so_warehouse_policy";
    private const string SettingView = "sys:branch-setting:view";
    private const string SettingEdit = "sys:branch-setting:edit";
    private const string BranchEdit = "sys:branch:edit";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Legacy read: the effective list as an array. An invalid fixed list is refused so the caller can tell.</summary>
    public async Task<List<ServingWarehouseDto>> GetServingWarehousesAsync(int branchId, CancellationToken cancellationToken = default)
    {
        await RequireReadableAsync(branchId, cancellationToken);
        var resolution = await ServingWarehouses.PolicyAsync(_context, branchId, cancellationToken);
        PostingTransaction.Require(!resolution.Invalid, "error.sd.warehouse_not_serving", 409);
        return resolution.Warehouses;
    }

    public async Task<SettingViewDto> GetServingPolicyAsync(int branchId, CancellationToken cancellationToken = default)
    {
        await RequireReadableAsync(branchId, cancellationToken);
        PostingTransaction.Require(_user.CanAt(SettingView, branchId), "error.branch_setting.not_found", 404);
        return View(await ServingWarehouses.LoadAsync(_context, branchId, cancellationToken), branchId);
    }

    public Task<SettingImpactDto> PreviewServingPolicyAsync(int branchId, UpdateServingWarehousesRequest request, string? ifMatch, CancellationToken cancellationToken = default)
    {
        PostingTransaction.Require(request.Mode != null, "error.branch_setting.invalid_value");
        return ChangeServingPolicyAsync(branchId, request, ifMatch, null, cancellationToken);
    }

    public Task<SettingViewDto> SaveServingPolicyAsync(int branchId, UpdateServingWarehousesRequest request, string? ifMatch, CancellationToken cancellationToken = default)
    {
        PostingTransaction.Require(request.Mode != null, "error.branch_setting.invalid_value");
        var posting = new PostingTransaction((ApplicationDbContext)_context);
        return posting.RunAsync(async () =>
        {
            await ChangeServingPolicyAsync(branchId, request, ifMatch, posting, cancellationToken);
            return View(await ServingWarehouses.LoadAsync(_context, branchId, cancellationToken), branchId);
        }, cancellationToken);
    }

    /// <summary>
    /// Legacy write: a list with rows is a fixed list, an empty list is the branch's own warehouses. Same permission,
    /// scope, stock guard and history as the new API; no entity tag or reason.
    /// </summary>
    public Task<List<ServingWarehouseDto>> UpdateServingWarehousesAsync(int branchId, UpdateServingWarehousesRequest request, CancellationToken cancellationToken = default)
    {
        var posting = new PostingTransaction((ApplicationDbContext)_context);
        return posting.RunAsync(async () =>
        {
            await ChangeServingPolicyAsync(branchId, new UpdateServingWarehousesRequest { Warehouses = request.Warehouses ?? [] }, null, posting, cancellationToken);
            return await ServingWarehouses.ForBranchAsync(_context, branchId, cancellationToken);
        }, cancellationToken);
    }

    /// <summary>Not found unless the unit is in the caller's data scope.</summary>
    private async Task RequireReadableAsync(int branchId, CancellationToken ct)
        => PostingTransaction.Require(await _context.Branches.AnyAsync(b => b.Id == branchId, ct), "error.branch_setting.not_found", 404);

    private bool CanEditAt(int branchId) => _user.CanAt(SettingEdit, branchId) && _user.CanAt(BranchEdit, branchId);

    /// <summary>Local state as stored in the history: null for inherit.</summary>
    private static string? LocalJson(ServingWarehouses.Tree tree, int branchId)
        => tree.Policies.GetValueOrDefault(branchId) is not { } policy ? null : JsonSerializer.Serialize(new
        {
            mode = ModeName(policy.Mode),
            warehouses = policy.Mode == WarehousePolicyMode.FixedList
                ? (tree.Lists.GetValueOrDefault(branchId) ?? []).OrderBy(l => l.WarehouseId).Select(l => new ServingWarehouseLine { WarehouseId = l.WarehouseId, IsDefault = l.IsDefault }).ToList()
                : null
        }, Json);

    /// <summary>
    /// Entity tag over what the view and the impact of a write depend on: ancestors, the unit and its subtree
    /// (parent links, active flags) and the revisions of their policies.
    /// </summary>
    private static string Etag(ServingWarehouses.Tree tree, int branchId)
    {
        var ids = new HashSet<int>(tree.Subtree(branchId));
        for (int? id = tree.Nodes.GetValueOrDefault(branchId)?.ParentId; id is int p && tree.Nodes.ContainsKey(p) && ids.Add(p); id = tree.Nodes[p].ParentId) { }
        var relevant = ids.Order().Select(id => $"{id}:{tree.Nodes[id].ParentId}:{tree.Nodes[id].IsActive}:{tree.Policies.GetValueOrDefault(id)?.Revision}");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(PolicyKey + "|" + string.Join(";", relevant)));
        return $"\"wms-{branchId}-{Convert.ToHexStringLower(hash, 0, 8)}\"";
    }

    private SettingViewDto View(ServingWarehouses.Tree tree, int branchId)
    {
        var resolution = ServingWarehouses.Resolve(tree, branchId);
        var local = LocalJson(tree, branchId);
        var source = new SettingSourceDto { Kind = resolution.SourceKind(branchId) };
        // Name the source only when the caller may see that unit.
        if (resolution.SourceBranchId is int sourceId && _user.CanAt(SettingView, sourceId))
        { source.BranchId = sourceId; source.BranchName = tree.Nodes[sourceId].Name; }
        return new SettingViewDto
        {
            BranchId = branchId,
            Key = PolicyKey,
            StorageKind = SettingDefinition.Module,
            ValueType = "object",
            EditorKey = PolicyKey,
            Local = local == null ? new SettingLocalDto() : new SettingLocalDto { Mode = "override", Value = JsonSerializer.Deserialize<JsonElement>(local) },
            Effective = resolution.Invalid
                ? new SettingEffectiveDto { Status = "invalid", ErrorCode = "error.sd.warehouse_not_serving", Source = source }
                : new SettingEffectiveDto
                {
                    Status = "resolved",
                    Value = JsonSerializer.SerializeToElement(new { mode = ModeName(resolution.Mode), warehouses = resolution.Warehouses, defaultWarehouseId = resolution.DefaultId }, Json),
                    Source = source
                },
            CanEdit = CanEditAt(branchId),
            Etag = Etag(tree, branchId)
        };
    }

    private static string ModeName(WarehousePolicyMode mode) => mode == WarehousePolicyMode.FixedList ? "fixed_list" : "own_warehouses";

    /// <summary>
    /// Works out what a save changes for the unit and every unit inheriting from it, refuses changes reaching units
    /// outside the caller's scope, and counts orders that hold stock in a warehouse their branch would lose.
    /// With <paramref name="posting"/> it also saves with a history row, under the company policy lock (order, then policy, then stock).
    /// </summary>
    private async Task<SettingImpactDto> ChangeServingPolicyAsync(int branchId, UpdateServingWarehousesRequest request, string? ifMatch, PostingTransaction? posting, CancellationToken ct)
    {
        var legacy = request.Mode == null;
        var lines = request.Warehouses ?? [];
        WarehousePolicyMode? mode = request.Mode switch
        {
            null => lines.Count > 0 ? WarehousePolicyMode.FixedList : WarehousePolicyMode.OwnWarehouses,
            "fixed_list" => WarehousePolicyMode.FixedList,
            "own_warehouses" => WarehousePolicyMode.OwnWarehouses,
            "inherit" => null,
            _ => throw new BusinessException("error.branch_setting.invalid_value", 400)
        };
        var invalidCode = legacy ? "error.validation.request" : "error.branch_setting.invalid_value";
        if (mode == WarehousePolicyMode.FixedList)
            PostingTransaction.Require(lines.Count is > 0 and <= 100 && lines.Select(l => l.WarehouseId).Distinct().Count() == lines.Count
                && lines.Count(l => l.IsDefault) == 1, invalidCode);
        else
            PostingTransaction.Require(lines.Count == 0, invalidCode);
        var reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        if (!legacy) PostingTransaction.Require(reason is { Length: <= 500 }, "error.branch_setting.reason_required");

        var target = await _context.Branches.AnyAsync(b => b.Id == branchId && b.IsActive
            && b.OrgUnitType != OrgUnitType.Warehouse, ct);   // A single-company install keeps its list on the root group, as in S27.1.
        PostingTransaction.Require(target, "error.branch_setting.not_found", 404);
        PostingTransaction.Require(CanEditAt(branchId), "error.access.denied", 403);
        if (!legacy) PostingTransaction.Require(!string.IsNullOrWhiteSpace(ifMatch), "error.branch_setting.revision_required", 428);
        if (posting != null) await posting.LockAsync(await ServingWarehouses.LockKeyAsync(_context, branchId, ct), ct);

        var tree = await ServingWarehouses.LoadAsync(_context, branchId, ct);
        if (!legacy) PostingTransaction.Require(ifMatch!.Trim() == Etag(tree, branchId), "error.branch_setting.stale_revision", 412);
        PostingTransaction.Require(lines.All(l => tree.Warehouses.ContainsKey(l.WarehouseId)), "error.sd.warehouse_not_serving", 409);
        var beforeLocal = LocalJson(tree, branchId);
        var units = tree.Subtree(branchId).Where(id => tree.Nodes[id].IsActive || id == branchId).ToList();
        var before = units.ToDictionary(id => id, id => ServingWarehouses.Resolve(tree, id));
        if (mode is WarehousePolicyMode m) tree.Policies[branchId] = new ServingWarehouses.Policy(m, 0); else tree.Policies.Remove(branchId);
        tree.Lists[branchId] = mode == WarehousePolicyMode.FixedList ? lines.Select(l => new ServingWarehouses.Line(l.WarehouseId, l.IsDefault)).ToList() : [];
        var after = units.ToDictionary(id => id, id => ServingWarehouses.Resolve(tree, id));
        var afterLocal = LocalJson(tree, branchId);

        var changed = units.Where(id => !before[id].UsableIds.SequenceEqual(after[id].UsableIds) || before[id].DefaultId != after[id].DefaultId
            || before[id].SourceBranchId != after[id].SourceBranchId || before[id].Invalid != after[id].Invalid).ToList();
        var scoped = (ApplicationDbContext)_context;
        PostingTransaction.Require(changed.All(id => (scoped.IsUnrestricted || scoped.AllowedBranchIds.Contains(id)) && _user.CanAt(SettingEdit, id)), "error.access.denied", 403);

        // Orders that hold stock in a warehouse their branch loses; a new default within the same list loses nothing.
        var lost = changed.Select(id => (Id: id, Lost: before[id].UsableIds.Except(after[id].UsableIds).ToArray()))
            .Where(p => p.Lost.Length > 0).ToDictionary(p => p.Id, p => p.Lost);
        var holding = 0;
        if (lost.Count > 0)
        {
            var branchIds = lost.Keys.ToArray();
            var warehouseIds = lost.Values.SelectMany(w => w).Distinct().ToArray();
            var held = await _context.StockReservations.IgnoreQueryFilters()
                .Where(r => r.Status == ReservationRecordStatus.Active && warehouseIds.Contains(r.WarehouseId))
                .Join(_context.SalesOrders.IgnoreQueryFilters().Where(o => !o.IsDeleted && branchIds.Contains(o.BranchId)), r => r.SalesOrderId, o => o.Id, (r, o) => new { o.Id, o.BranchId, r.WarehouseId })
                .Distinct().ToListAsync(ct);
            holding = held.Where(h => lost[h.BranchId].Contains(h.WarehouseId)).Select(h => h.Id).Distinct().Count();
        }
        var impact = new SettingImpactDto
        {
            Blocked = holding > 0,
            AffectedBranchCount = changed.Count,
            Changes = changed.Select(id => new SettingImpactChangeDto { BranchId = id, BranchName = tree.Nodes[id].Name, Before = Side(before[id], id), After = Side(after[id], id) }).ToList(),
            Blockers = holding > 0 ? [new SettingBlockerDto { Code = "error.branch_setting.active_reservations", Count = holding }] : []
        };
        if (posting == null) return impact;
        PostingTransaction.Require(holding == 0, "error.branch_setting.active_reservations", 409);
        // Saving the same local state again is a no-op without a history row.
        if (beforeLocal == afterLocal) return impact;

        var policy = await _context.BranchWarehousePolicies.SingleOrDefaultAsync(p => p.BranchId == branchId, ct);
        // Revisions keep counting through "inherit", so an entity tag never comes back after a change.
        var revision = policy?.Revision + 1 ?? (await ((ApplicationDbContext)_context).BranchSettingHistories.Where(h => h.BranchId == branchId && h.SettingKey == PolicyKey)
            .MaxAsync(h => (int?)h.Revision, ct) ?? 0) + 1;
        if (mode is not WarehousePolicyMode newMode) { if (policy != null) { policy.IsDeleted = true; policy.Revision = revision; } }
        else if (policy == null) _context.BranchWarehousePolicies.Add(new BranchWarehousePolicy { BranchId = branchId, Mode = newMode, Revision = revision });
        else { policy.Mode = newMode; policy.Revision = revision; }
        ((ApplicationDbContext)_context).BranchSettingHistories.Add(new BranchSettingHistory
        { BranchId = branchId, SettingKey = PolicyKey, BeforeJson = beforeLocal, AfterJson = afterLocal, ChangedBy = _user.UserId, Reason = reason, Revision = revision });

        // Only a fixed list keeps rows, so S27.1 code reads the same lists after a rollback.
        var keep = mode == WarehousePolicyMode.FixedList ? lines.Select(l => l.WarehouseId).ToArray() : [];
        var current = await _context.BranchServingWarehouses.Where(s => s.BranchId == branchId).ToListAsync(ct);
        // Clear the old default first: the one-default index is checked row by row.
        foreach (var row in current) { row.IsDefault = false; if (!keep.Contains(row.WarehouseId)) row.IsDeleted = true; }
        await _context.SaveChangesAsync(ct);
        foreach (var line in keep.Length > 0 ? lines : [])
        {
            var row = current.FirstOrDefault(r => r.WarehouseId == line.WarehouseId && !r.IsDeleted);
            if (row == null) _context.BranchServingWarehouses.Add(row = new BranchServingWarehouse { BranchId = branchId, WarehouseId = line.WarehouseId });
            row.IsDefault = line.IsDefault;
        }
        await _context.SaveChangesAsync(ct);
        return impact;
    }

    private static SettingImpactSideDto Side(ServingWarehouses.Resolution r, int branchId) => new()
    {
        SourceKind = r.SourceKind(branchId),
        Value = r.Invalid ? null : JsonSerializer.SerializeToElement(new { warehouseIds = r.UsableIds, defaultWarehouseId = r.DefaultId }, Json)
    };

    public async Task<List<StockAvailabilityDto>> GetAvailabilityAsync(int branchId, int warehouseId, IReadOnlyCollection<int> itemIds, CancellationToken cancellationToken = default)
    {
        PostingTransaction.Require(itemIds.Count is > 0 and <= 200, "error.validation.request");
        await ServingWarehouses.RequireAsync(_context, branchId, warehouseId, cancellationToken);
        var balances = await _context.StockBalances.IgnoreQueryFilters().AsNoTracking()
            .Where(b => !b.IsDeleted && b.WarehouseId == warehouseId && itemIds.Contains(b.ItemId))
            .ToDictionaryAsync(b => b.ItemId, cancellationToken);
        // An item never received in the warehouse has no balance row and nothing available.
        return itemIds.Distinct().Select(id => balances.TryGetValue(id, out var b)
            ? new StockAvailabilityDto { ItemId = id, QuantityOnHand = b.QuantityOnHand, QuantityReserved = b.QuantityReserved, QuantityAvailable = b.QuantityOnHand - b.QuantityReserved }
            : new StockAvailabilityDto { ItemId = id }).ToList();
    }
}
