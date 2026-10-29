using ERPm.Application.Common;
using ERPm.Application.DTOs.Catalog;
using ERPm.Application.Features.Catalog;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers;

public class ItemsController : BaseApiController
{
    private readonly IItemService _itemService;

    public ItemsController(IItemService itemService)
    {
        _itemService = itemService;
    }

    /// <summary>
    /// Gets a paginated list of items filtered by keyword, category, and item type.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ItemDto>>>> GetItems([FromQuery] ItemFilterQuery query, CancellationToken cancellationToken)
    {
        var result = await _itemService.GetItemsAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<ItemDto>>.Ok(result));
    }

    /// <summary>
    /// Gets details of a single item by its internal integer ID (including JSONB Translations and Attributes).
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<ItemDto>>> GetItemById(int id, CancellationToken cancellationToken)
    {
        var result = await _itemService.GetItemByIdAsync(id, cancellationToken);
        if (result == null)
        {
            return NotFound(ApiResponse<ItemDto>.Fail("error.validation.reference"));
        }
        return Ok(ApiResponse<ItemDto>.Ok(result));
    }

    /// <summary>
    /// Creates a new item or service.
    /// </summary>
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "Admin")]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ItemDto>>> CreateItem([FromBody] CreateItemRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(ApiResponse<ItemDto>.Fail("error.validation.reference"));
        }

        var result = await _itemService.CreateItemAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetItemById), new { id = result.Id }, ApiResponse<ItemDto>.Ok(result, "common.saved"));
    }

    /// <summary>
    /// Updates an existing item by its internal integer ID.
    /// </summary>
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<ItemDto>>> UpdateItem(int id, [FromBody] UpdateItemRequest request, CancellationToken cancellationToken)
    {
        var result = await _itemService.UpdateItemAsync(id, request, cancellationToken);
        if (result == null)
        {
            return NotFound(ApiResponse<ItemDto>.Fail("error.validation.reference"));
        }
        return Ok(ApiResponse<ItemDto>.Ok(result, "common.saved"));
    }

    /// <summary>
    /// Soft-deletes an item by its internal integer ID.
    /// </summary>
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteItem(int id, CancellationToken cancellationToken)
    {
        var success = await _itemService.DeleteItemAsync(id, cancellationToken);
        if (!success)
        {
            return NotFound(ApiResponse<bool>.Fail("error.validation.reference"));
        }
        return Ok(ApiResponse<bool>.Ok(true, "common.saved"));
    }
}
