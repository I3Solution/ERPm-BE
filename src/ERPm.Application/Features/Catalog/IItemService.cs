using ERPm.Application.Common;
using ERPm.Application.DTOs.Catalog;

namespace ERPm.Application.Features.Catalog;

public interface IItemService
{
    Task<PagedResult<ItemDto>> GetItemsAsync(ItemFilterQuery query, CancellationToken cancellationToken = default);
    Task<ItemDto?> GetItemByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ItemDto> CreateItemAsync(CreateItemRequest request, CancellationToken cancellationToken = default);
    Task<ItemDto?> UpdateItemAsync(int id, UpdateItemRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteItemAsync(int id, CancellationToken cancellationToken = default);
}
