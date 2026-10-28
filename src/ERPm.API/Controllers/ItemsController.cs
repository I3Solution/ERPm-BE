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
    /// Lấy danh sách hàng hóa phân trang, có lọc theo từ khóa, danh mục, loại hàng
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<ItemDto>>>> GetItems([FromQuery] ItemFilterQuery query, CancellationToken cancellationToken)
    {
        var result = await _itemService.GetItemsAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<ItemDto>>.Ok(result));
    }

    /// <summary>
    /// Lấy thông tin chi tiết một sản phẩm theo ID (kèm Translations JSONB)
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ItemDto>>> GetItemById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _itemService.GetItemByIdAsync(id, cancellationToken);
        if (result == null)
        {
            return NotFound(ApiResponse<ItemDto>.Fail($"Item with ID '{id}' was not found."));
        }
        return Ok(ApiResponse<ItemDto>.Ok(result));
    }

    /// <summary>
    /// Tạo mới một sản phẩm / dịch vụ
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ItemDto>>> CreateItem([FromBody] CreateItemRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(ApiResponse<ItemDto>.Fail("Item code and name are required."));
        }

        var result = await _itemService.CreateItemAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetItemById), new { id = result.Id }, ApiResponse<ItemDto>.Ok(result, "Item created successfully."));
    }

    /// <summary>
    /// Cập nhật thông tin sản phẩm
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<ItemDto>>> UpdateItem(Guid id, [FromBody] UpdateItemRequest request, CancellationToken cancellationToken)
    {
        var result = await _itemService.UpdateItemAsync(id, request, cancellationToken);
        if (result == null)
        {
            return NotFound(ApiResponse<ItemDto>.Fail($"Item with ID '{id}' was not found."));
        }
        return Ok(ApiResponse<ItemDto>.Ok(result, "Item updated successfully."));
    }

    /// <summary>
    /// Xóa mềm một sản phẩm (Soft-delete)
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<bool>>> DeleteItem(Guid id, CancellationToken cancellationToken)
    {
        var success = await _itemService.DeleteItemAsync(id, cancellationToken);
        if (!success)
        {
            return NotFound(ApiResponse<bool>.Fail($"Item with ID '{id}' was not found."));
        }
        return Ok(ApiResponse<bool>.Ok(true, "Item deleted successfully."));
    }
}
