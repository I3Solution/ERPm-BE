using ERPm.Application.DTOs.Sales;

namespace ERPm.Application.Common.Crud;

/// <summary>Works out selling prices from the price lists applicable to a customer and branch.</summary>
public interface IPricingService
{
    Task<CalculatePriceResponse> CalculateAsync(CalculatePriceRequest request, CancellationToken cancellationToken = default);
}
