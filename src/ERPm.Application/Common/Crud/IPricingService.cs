using ERPm.Application.DTOs.Sales;

namespace ERPm.Application.Common.Crud;

public interface IPricingService
{
    Task<CalculatePriceResponse> CalculateAsync(CalculatePriceRequest request, CancellationToken cancellationToken = default);
    Task<List<PriceListVersionDto>> GetVersionsAsync(int priceListId, CancellationToken cancellationToken = default);
    Task<PriceListVersionDto> GetVersionAsync(int priceListId, int versionId, CancellationToken cancellationToken = default);
    Task<List<PriceListVersionAuditDto>> GetVersionAuditAsync(int priceListId, int versionId, CancellationToken cancellationToken = default);
    Task<PriceListVersionDto> CreateVersionAsync(int priceListId, CreatePriceListVersionRequest request, CancellationToken cancellationToken = default);
    Task<PriceListVersionDto> UpdateVersionAsync(int priceListId, int versionId, UpdatePriceListVersionRequest request, CancellationToken cancellationToken = default);
    Task DeleteVersionAsync(int priceListId, int versionId, CancellationToken cancellationToken = default);
    Task<PriceListVersionDto> SubmitVersionAsync(int priceListId, int versionId, PriceListVersionTransitionRequest? request = null, CancellationToken cancellationToken = default);
    Task<PriceListVersionDto> WithdrawVersionAsync(int priceListId, int versionId, PriceListVersionTransitionRequest? request = null, CancellationToken cancellationToken = default);
    Task<PriceListVersionDto> ApproveVersionAsync(int priceListId, int versionId, PriceListVersionTransitionRequest? request = null, CancellationToken cancellationToken = default);
    Task<PriceListVersionDto> RejectVersionAsync(int priceListId, int versionId, PriceListVersionTransitionRequest request, CancellationToken cancellationToken = default);
    Task<PriceListVersionDto> ActivateVersionAsync(int priceListId, int versionId, PriceListVersionTransitionRequest? request = null, CancellationToken cancellationToken = default);
    Task<PriceListVersionDto> CloseVersionAsync(int priceListId, int versionId, PriceListVersionTransitionRequest request, CancellationToken cancellationToken = default);
    Task<PriceListVersionDto> ArchiveVersionAsync(int priceListId, int versionId, PriceListVersionTransitionRequest? request = null, CancellationToken cancellationToken = default);
    Task<PriceListItemDto> AddVersionItemAsync(int priceListId, int versionId, CreatePriceListItemRequest request, CancellationToken cancellationToken = default);
    Task<PriceListItemDto> UpdateVersionItemAsync(int priceListId, int versionId, int itemId, UpdatePriceListItemRequest request, CancellationToken cancellationToken = default);
    Task DeleteVersionItemAsync(int priceListId, int versionId, int itemId, CancellationToken cancellationToken = default);
    Task<PriceListVersionDto> CloneVersionAsync(int priceListId, ClonePriceListVersionRequest request, CancellationToken cancellationToken = default);
    Task<BulkPriceAdjustmentResponse> BulkAdjustVersionAsync(int priceListId, int versionId, BulkPriceAdjustmentRequest request, CancellationToken cancellationToken = default);
}
