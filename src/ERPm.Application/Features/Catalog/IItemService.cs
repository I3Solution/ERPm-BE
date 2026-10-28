using ERPm.Application.Common;
using ERPm.Application.DTOs.Catalog;

namespace ERPm.Application.Features.Catalog;

public interface IItemService
{
    Task<PagedResult<ItemDto>> GetItemsAsync(ItemFilterQuery query, CancellationToken cancellationToken = default);
    Task<ItemDto?> GetItemByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ItemDto> CreateItemAsync(CreateItemRequest request, CancellationToken cancellationToken = default);
    Task<ItemDto?> UpdateItemAsync(Guid id, UpdateItemRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteItemAsync(Guid id, CancellationToken cancellationToken = default);
}
