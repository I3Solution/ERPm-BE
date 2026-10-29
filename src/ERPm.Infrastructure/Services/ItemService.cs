using ERPm.Application.Common;
using ERPm.Application.Common.Interfaces;
using ERPm.Application.DTOs.Catalog;
using ERPm.Application.Features.Catalog;
using ERPm.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;

namespace ERPm.Infrastructure.Services;

public class ItemService : IItemService
{
    private readonly IApplicationDbContext _context;

    public ItemService(IApplicationDbContext context)
    {
        _context = context;
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
            dbQuery = dbQuery.Where(i => i.Name.ToLower().Contains(keyword) ||
                                         i.Code.ToLower().Contains(keyword) ||
                                         (i.Barcode != null && i.Barcode.ToLower().Contains(keyword)));
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

        var pageIndex = Math.Max(1, query.PageIndex);
        var pageSize = Math.Max(1, query.PageSize);

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
        var item = new Item
        {
            Code = request.Code,
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

    public async Task<ItemDto?> UpdateItemAsync(int id, UpdateItemRequest request, CancellationToken cancellationToken = default)
    {
        var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item == null) return null;

        item.Code = request.Code;
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
    }

    public async Task<bool> DeleteItemAsync(int id, CancellationToken cancellationToken = default)
    {
        var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item == null) return false;

        // Soft delete according to project standard
        item.IsDeleted = true;
        item.DeletedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
