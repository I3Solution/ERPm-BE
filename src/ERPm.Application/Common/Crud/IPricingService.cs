using ERPm.Application.DTOs.Sales;

namespace ERPm.Application.Common.Crud;

public interface IPricingService
{
    Task<CalculatePriceResponse> CalculateAsync(CalculatePriceRequest request, CancellationToken cancellationToken = default);
    Task<List<PriceListVersionDto>> GetVersionsAsync(int priceListId, CancellationToken cancellationToken = default);
    Task<PriceListVersionDto> GetVersionAsync(int priceListId, int versionId, CancellationToken cancellationToken = default);
    Task<PriceListVersionDto> CloneVersionAsync(int priceListId, ClonePriceListVersionRequest request, CancellationToken cancellationToken = default);
    Task<BulkPriceAdjustmentResponse> BulkAdjustVersionAsync(int priceListId, int versionId, BulkPriceAdjustmentRequest request, CancellationToken cancellationToken = default);
}
