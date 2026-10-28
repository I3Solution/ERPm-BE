using ERPm.Application.DTOs.Partner;

namespace ERPm.Application.Features.Partner;

public interface IPartnerService
{
    Task<List<PartnerDto>> GetPartnersAsync(CancellationToken cancellationToken = default);
    Task<PartnerDto> CreatePartnerAsync(CreatePartnerRequest request, CancellationToken cancellationToken = default);
}
