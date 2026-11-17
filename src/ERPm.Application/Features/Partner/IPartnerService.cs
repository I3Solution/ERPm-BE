using ERPm.Application.DTOs.Partner;

namespace ERPm.Application.Features.Partner;

public interface IPartnerService
{
    Task<List<PartnerDto>> GetPartnersAsync(ERPm.Application.Common.Crud.ListSort? sort = null, CancellationToken cancellationToken = default);
    /// <param name="type">null/all, customer (Customer or Both) or vendor (Vendor or Both).</param>
    Task<PartnerPageDto> GetPartnersPageAsync(int pageIndex, int pageSize, string? q, string? type, ERPm.Application.Common.Crud.ListSort? sort = null, CancellationToken cancellationToken = default);
    Task<PartnerDto> GetPartnerAsync(int partnerId, CancellationToken cancellationToken = default);
    Task<PartnerDto> UpdatePartnerAsync(int partnerId, UpdatePartnerRequest request, CancellationToken cancellationToken = default);
    Task<PartnerDto> CreatePartnerAsync(CreatePartnerRequest request, CancellationToken cancellationToken = default);
    Task<PartnerDto> SetUnlimitedCreditAsync(int partnerId, bool? isUnlimitedCredit, CancellationToken cancellationToken = default);
}
