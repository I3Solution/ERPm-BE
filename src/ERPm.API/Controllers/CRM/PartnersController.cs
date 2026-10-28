using ERPm.Application.Common;
using ERPm.Application.DTOs.Partner;
using ERPm.Application.Features.Partner;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers;

public class PartnersController : BaseApiController
{
    private readonly IPartnerService _partnerService;

    public PartnersController(IPartnerService partnerService)
    {
        _partnerService = partnerService;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<PartnerDto>>>> GetPartners(CancellationToken cancellationToken)
    {
        var result = await _partnerService.GetPartnersAsync(cancellationToken);
        return Ok(ApiResponse<List<PartnerDto>>.Ok(result));
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<PartnerDto>>> CreatePartner([FromBody] CreatePartnerRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(ApiResponse<PartnerDto>.Fail("Partner code and name are required."));
        }

        var result = await _partnerService.CreatePartnerAsync(request, cancellationToken);
        return Ok(ApiResponse<PartnerDto>.Ok(result, "Partner created successfully."));
    }
}
