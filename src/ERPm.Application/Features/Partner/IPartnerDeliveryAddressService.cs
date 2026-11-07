using ERPm.Application.DTOs.Partner;

namespace ERPm.Application.Features.Partner;

public interface IPartnerDeliveryAddressService
{
    Task<List<DeliveryAddressDto>> GetAsync(int partnerId, CancellationToken cancellationToken = default);
    Task<DeliveryAddressDto> GetByIdAsync(int partnerId, int id, CancellationToken cancellationToken = default);
    Task<DeliveryAddressDto> CreateAsync(int partnerId, SaveDeliveryAddressRequest request, CancellationToken cancellationToken = default);
    Task<DeliveryAddressDto> UpdateAsync(int partnerId, int id, SaveDeliveryAddressRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int partnerId, int id, CancellationToken cancellationToken = default);
    Task<DeliveryAddressDto> SetDefaultAsync(int partnerId, int id, CancellationToken cancellationToken = default);
}
