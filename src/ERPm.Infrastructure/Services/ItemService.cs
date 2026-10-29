using ERPm.Application.Common;
using ERPm.Application.Common.Interfaces;
using ERPm.Application.DTOs.Catalog;
using ERPm.Application.Features.Catalog;
using ERPm.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;
using ERPm.Domain.Common;
using static ERPm.Infrastructure.Services.Common.PostingTransaction;
using ERPm.Infrastructure.Services.Common;

namespace ERPm.Infrastructure.Services;

public class ItemService : IItemService
{
    private readonly IApplicationDbContext _context;
    private readonly PostingTransaction _posting;

    public ItemService(IApplicationDbContext context, PostingTransaction posting)
    {
        _context = context;
        _posting = posting;
    }

    public async Task<PagedResult<ItemDto>> GetItemsAsync(ItemFilterQuery query, CancellationToken cancellationToken = default)
    {
        var dbQuery = _context.Items
            .Include(i => i.Category)
            .Include(i => i.BaseUom)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim().ToLower();
            var pattern = "%" + keyword.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
            dbQuery = _context.Items.FromSqlInterpolated($"SELECT * FROM \"tbl_WMS_Item\" WHERE name ILIKE {pattern} OR code ILIKE {pattern} OR barcode ILIKE {pattern} OR COALESCE(translations->>'en', '') ILIKE {pattern}")
                .Include(i => i.Category).Include(i => i.BaseUom).AsNoTracking();
        }

        if (query.CategoryId.HasValue)
        {
            dbQuery = dbQuery.Where(i => i.CategoryId == query.CategoryId.Value);
        }

        if (query.ItemType.HasValue)
        {
            dbQuery = dbQuery.Where(i => i.ItemType == query.ItemType.Value);
        }

        if (query.IsActive.HasValue)
        {
            dbQuery = dbQuery.Where(i => i.IsActive == query.IsActive.Value);
        }

        var totalCount = await dbQuery.CountAsync(cancellationToken);

        var pageIndex = Math.Clamp(query.PageIndex, 1, 1000000);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var items = await dbQuery
            .OrderBy(i => i.Code)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new ItemDto
            {
                Id = i.Id,
                Code = i.Code,
                Barcode = i.Barcode,
                Name = i.Name,
                Translations = i.Translations,
                ItemType = i.ItemType,
                CategoryId = i.CategoryId,
                CategoryName = i.Category != null ? i.Category.Name : null,
                BaseUomId = i.BaseUomId,
                BaseUomName = i.BaseUom != null ? i.BaseUom.Name : null,
                SalePrice = i.SalePrice,
                CostPrice = i.CostPrice,
                MinStockAlert = i.MinStockAlert,
                MaxStockAlert = i.MaxStockAlert,
                ImageUrl = i.ImageUrl,
                Description = i.Description,
                IsActive = i.IsActive,
                Attributes = i.Attributes
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<ItemDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    public async Task<ItemDto?> GetItemByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var item = await _context.Items
            .Include(i => i.Category)
            .Include(i => i.BaseUom)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (item == null) return null;

        return new ItemDto
        {
            Id = item.Id,
            Code = item.Code,
            Barcode = item.Barcode,
            Name = item.Name,
            Translations = item.Translations,
            ItemType = item.ItemType,
            CategoryId = item.CategoryId,
            CategoryName = item.Category?.Name,
            BaseUomId = item.BaseUomId,
            BaseUomName = item.BaseUom?.Name,
            SalePrice = item.SalePrice,
            CostPrice = item.CostPrice,
            MinStockAlert = item.MinStockAlert,
            MaxStockAlert = item.MaxStockAlert,
            ImageUrl = item.ImageUrl,
            Description = item.Description,
            IsActive = item.IsActive,
            Attributes = item.Attributes
        };
    }

    public async Task<ItemDto> CreateItemAsync(CreateItemRequest request, CancellationToken cancellationToken = default)
    {
        await ValidateAsync(request, cancellationToken);
        var item = new Item
        {
            Code = string.IsNullOrWhiteSpace(request.Code) ? $"SP{Uuid7Generator.NewUuid7():N}" : request.Code.Trim().ToUpperInvariant(),
            Barcode = request.Barcode,
            Name = request.Name,
            Translations = request.Translations ?? new(),
            ItemType = request.ItemType,
            CategoryId = request.CategoryId,
            BaseUomId = request.BaseUomId,
            SalePrice = request.SalePrice,
            CostPrice = request.CostPrice,
            MinStockAlert = request.MinStockAlert,
            MaxStockAlert = request.MaxStockAlert,
            ImageUrl = request.ImageUrl,
            Description = request.Description,
            IsActive = true,
            Attributes = request.Attributes ?? new()
        };

        await _context.Items.AddAsync(item, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return (await GetItemByIdAsync(item.Id, cancellationToken))!;
    }

    public Task<ItemDto?> UpdateItemAsync(int id, UpdateItemRequest request, CancellationToken cancellationToken = default) => _posting.RunAsync<ItemDto?>(async () =>
    {
        await _posting.LockAsync($"item:{id}", cancellationToken);
        await ValidateAsync(request, cancellationToken);
        var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item == null) return null;

        Require(item.ItemType == request.ItemType && item.BaseUomId == request.BaseUomId || !await HasFinancialHistoryAsync(id, cancellationToken), "error.item.in_use", 409);

        if (!string.IsNullOrWhiteSpace(request.Code)) item.Code = request.Code.Trim().ToUpperInvariant();
        item.Barcode = request.Barcode;
        item.Name = request.Name;
        if (request.Translations != null)
        {
            item.Translations = request.Translations;
        }
        item.ItemType = request.ItemType;
        item.CategoryId = request.CategoryId;
        item.BaseUomId = request.BaseUomId;
        item.SalePrice = request.SalePrice;
        item.CostPrice = request.CostPrice;
        item.MinStockAlert = request.MinStockAlert;
        item.MaxStockAlert = request.MaxStockAlert;
        item.ImageUrl = request.ImageUrl;
        item.Description = request.Description;
        item.IsActive = request.IsActive;
        if (request.Attributes != null)
        {
            item.Attributes = request.Attributes;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return await GetItemByIdAsync(item.Id, cancellationToken);
    }, cancellationToken);

    public Task<bool> DeleteItemAsync(int id, CancellationToken cancellationToken = default) => _posting.RunAsync(async () =>
    {
        await _posting.LockAsync($"item:{id}", cancellationToken);
        var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item == null) return false;
        Require(!await HasFinancialHistoryAsync(id, cancellationToken), "error.item.in_use", 409);

        // Soft delete according to project standard
        item.IsDeleted = true;
        item.DeletedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }, cancellationToken);

    private async Task<bool> HasFinancialHistoryAsync(int id, CancellationToken ct) =>
        await _context.StockBalances.IgnoreQueryFilters().AnyAsync(b => b.ItemId == id, ct) ||
        await _context.StockReceiptItems.IgnoreQueryFilters().AnyAsync(i => i.ItemId == id, ct) ||
        await _context.StockIssueItems.IgnoreQueryFilters().AnyAsync(i => i.ItemId == id, ct) ||
        await _context.SalesInvoiceItems.IgnoreQueryFilters().AnyAsync(i => i.ItemId == id, ct);

    private async Task ValidateAsync(CreateItemRequest request, CancellationToken ct)
    {
        Require(!string.IsNullOrWhiteSpace(request.Name) && request.Name.Trim().Length <= 255 && request.Code.Length <= 50 && (request.Barcode?.Length ?? 0) <= 100 && Enum.IsDefined(request.ItemType), "error.validation.request");
        ValidateNumber(request.SalePrice); ValidateNumber(request.CostPrice); ValidateNumber(request.MinStockAlert); ValidateNumber(request.MaxStockAlert);
        Require(request.MaxStockAlert == 0 || request.MaxStockAlert >= request.MinStockAlert, "error.validation.request");
        Require(await _context.UnitsOfMeasure.AnyAsync(u => u.Id == request.BaseUomId && u.IsActive, ct), "error.validation.reference");
        Require(request.CategoryId == null || await _context.Categories.AnyAsync(c => c.Id == request.CategoryId && c.IsActive, ct), "error.validation.reference");
        var definitions = await _context.ItemAttributes.Where(a => a.CategoryId == null || a.CategoryId == request.CategoryId).ToListAsync(ct);
        foreach (var definition in definitions.GroupBy(a => a.Code).Select(g => g.OrderByDescending(a => a.CategoryId.HasValue).First()))
        {
            var exists = request.Attributes != null && request.Attributes.TryGetValue(definition.Code, out _);
            Require(exists || !definition.IsRequired, "error.validation.required");
            if (!exists) continue;
            var value = System.Text.Json.JsonSerializer.SerializeToElement(request.Attributes![definition.Code]);
            var valid = definition.DataType switch
            {
                AttributeDataType.Boolean => value.ValueKind is System.Text.Json.JsonValueKind.True or System.Text.Json.JsonValueKind.False,
                AttributeDataType.Number => value.ValueKind == System.Text.Json.JsonValueKind.Number && value.TryGetDecimal(out _),
                AttributeDataType.Select => value.ValueKind == System.Text.Json.JsonValueKind.String && definition.Options.Contains(value.GetString()!),
                AttributeDataType.Date => value.ValueKind == System.Text.Json.JsonValueKind.String && DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _),
                _ => value.ValueKind == System.Text.Json.JsonValueKind.String && (!definition.IsRequired || !string.IsNullOrWhiteSpace(value.GetString()))
            };
            Require(valid, "error.validation.request");
        }
    }
}
