using ERPm.API.Security;
using ERPm.Application.Common;
using ERPm.Application.DTOs.Partner;
using ERPm.Application.Features.Partner;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers;

public class PartnersController : BaseApiController
{
    private readonly IPartnerService _partnerService;
    private readonly IPartnerDeliveryAddressService _addresses;

    public PartnersController(IPartnerService partnerService, IPartnerDeliveryAddressService addresses)
    {
        _partnerService = partnerService;
        _addresses = addresses;
    }

    [RequirePermission("crm:partner:view")]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<PartnerDto>>>> GetPartners(CancellationToken cancellationToken)
    {
        var result = await _partnerService.GetPartnersAsync(cancellationToken);
        return Ok(ApiResponse<List<PartnerDto>>.Ok(result));
    }

    [RequirePermission("crm:partner:view")]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<PartnerDto>>> GetPartner(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<PartnerDto>.Ok(await _partnerService.GetPartnerAsync(id, cancellationToken)));

    // The catalog has no separate edit right for partners yet, so editing needs the create right like the delivery addresses.
    [RequirePermission("crm:partner:create")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<PartnerDto>>> UpdatePartner(int id, [FromBody] UpdatePartnerRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<PartnerDto>.Ok(await _partnerService.UpdatePartnerAsync(id, request, cancellationToken), "common.saved"));

    [RequirePermission("crm:partner:create")]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<PartnerDto>>> CreatePartner([FromBody] CreatePartnerRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return BadRequest(ApiResponse<PartnerDto>.Fail("error.validation.reference"));
        }

        var result = await _partnerService.CreatePartnerAsync(request, cancellationToken);
        return Ok(ApiResponse<PartnerDto>.Ok(result, "common.saved"));
    }

    /// <summary>Switches the "no credit limit" flag; the service also checks the dedicated right.</summary>
    [RequirePermission("crm:partner:unlimited-credit")]
    [HttpPut("{id:int}/unlimited-credit")]
    public async Task<ActionResult<ApiResponse<PartnerDto>>> SetUnlimitedCredit(int id, [FromBody] SetUnlimitedCreditRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<PartnerDto>.Ok(await _partnerService.SetUnlimitedCreditAsync(id, request.IsUnlimitedCredit, cancellationToken), "common.saved"));

    [RequirePermission("crm:partner:view")]
    [HttpGet("{partnerId:int}/delivery-addresses")]
    public async Task<ActionResult<ApiResponse<List<DeliveryAddressDto>>>> GetDeliveryAddresses(int partnerId, CancellationToken cancellationToken) =>
        Ok(ApiResponse<List<DeliveryAddressDto>>.Ok(await _addresses.GetAsync(partnerId, cancellationToken)));

    [RequirePermission("crm:partner:view")]
    [HttpGet("{partnerId:int}/delivery-addresses/{id:int}")]
    public async Task<ActionResult<ApiResponse<DeliveryAddressDto>>> GetDeliveryAddress(int partnerId, int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<DeliveryAddressDto>.Ok(await _addresses.GetByIdAsync(partnerId, id, cancellationToken)));

    [RequirePermission("crm:partner:create")]
    [HttpPost("{partnerId:int}/delivery-addresses")]
    public async Task<ActionResult<ApiResponse<DeliveryAddressDto>>> CreateDeliveryAddress(int partnerId, [FromBody] SaveDeliveryAddressRequest request, CancellationToken cancellationToken)
    {
        var created = await _addresses.CreateAsync(partnerId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<DeliveryAddressDto>.Ok(created, "common.saved"));
    }

    [RequirePermission("crm:partner:create")]
    [HttpPut("{partnerId:int}/delivery-addresses/{id:int}")]
    public async Task<ActionResult<ApiResponse<DeliveryAddressDto>>> UpdateDeliveryAddress(int partnerId, int id, [FromBody] SaveDeliveryAddressRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<DeliveryAddressDto>.Ok(await _addresses.UpdateAsync(partnerId, id, request, cancellationToken), "common.saved"));

    [RequirePermission("crm:partner:create")]
    [HttpDelete("{partnerId:int}/delivery-addresses/{id:int}")]
    public async Task<IActionResult> DeleteDeliveryAddress(int partnerId, int id, CancellationToken cancellationToken)
    {
        await _addresses.DeleteAsync(partnerId, id, cancellationToken);
        return NoContent();
    }

    [RequirePermission("crm:partner:create")]
    [HttpPost("{partnerId:int}/delivery-addresses/{id:int}/set-default")]
    public async Task<ActionResult<ApiResponse<DeliveryAddressDto>>> SetDefaultDeliveryAddress(int partnerId, int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<DeliveryAddressDto>.Ok(await _addresses.SetDefaultAsync(partnerId, id, cancellationToken), "common.saved"));
}
