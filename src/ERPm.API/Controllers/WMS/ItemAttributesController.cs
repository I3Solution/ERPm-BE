using ERPm.API.Controllers.Base;
using ERPm.Application.Common;
using ERPm.Application.Common.Crud;
using ERPm.Application.Common.Interfaces;
using ERPm.Application.DTOs.Catalog;
using ERPm.Domain.Entities.Catalog;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ERPm.API.Controllers.WMS;

/// <summary>
/// Controller for managing dynamic item attributes (Schema-Driven Metadata) in WMS module.
/// Inherits BaseCrudController to automatically provide full type-safe CRUD operations.
/// </summary>
[Route("api/v1/wms/item-attributes")]
public class ItemAttributesController : BaseCrudController<ItemAttribute, ItemAttributeDto, CreateItemAttributeRequest, UpdateItemAttributeRequest>
{
    private readonly IApplicationDbContext _context;

    public ItemAttributesController(
        IBaseCrudService<ItemAttribute, ItemAttributeDto, CreateItemAttributeRequest, UpdateItemAttributeRequest> service,
        IApplicationDbContext context)
        : base(service)
    {
        _context = context;
    }

    /// <summary>
    /// Gets all dynamic attributes applicable to a given category plus global attributes (CategoryId == null).
    /// </summary>
    [HttpGet("by-category")]
    [HttpGet("by-category/{categoryId:guid}")]
    public async Task<IActionResult> GetByCategory([FromRoute] Guid? categoryId, CancellationToken cancellationToken = default)
    {
        var attributes = await _context.ItemAttributes
            .Include(a => a.Category)
            .Where(a => a.CategoryId == null || (categoryId.HasValue && a.CategoryId == categoryId.Value))
            .OrderBy(a => a.SortOrder)
            .Select(a => new ItemAttributeDto
            {
                Id = a.Id,
                CategoryId = a.CategoryId,
                CategoryName = a.Category != null ? a.Category.Name : null,
                Code = a.Code,
                Name = a.Name,
                DataType = a.DataType,
                Options = a.Options,
                Unit = a.Unit,
                IsRequired = a.IsRequired,
                IsFilterable = a.IsFilterable,
                SortOrder = a.SortOrder
            })
            .ToListAsync(cancellationToken);

        return Ok(ApiResponse<List<ItemAttributeDto>>.Ok(attributes));
    }
}
