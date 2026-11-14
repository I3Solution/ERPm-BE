using ERPm.API.Controllers.Base;
using ERPm.API.Security;
using ERPm.Application.Common;
using ERPm.Application.Common.Crud;
using ERPm.Application.DTOs.Sales;
using ERPm.Domain.Entities.Sales;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers.SALES;

[PermissionResource("sd:pricelist")]
[Route("api/v1/sales/price-lists")]
public class PriceListsController : BaseCrudController<PriceList, PriceListDetailDto, CreatePriceListRequest, UpdatePriceListRequest>
{
    private readonly IPricingService _pricing;

    public PriceListsController(IBaseCrudService<PriceList, PriceListDetailDto, CreatePriceListRequest, UpdatePriceListRequest> service, IPricingService pricing)
        : base(service)
    {
        _pricing = pricing;
    }

    [RequirePermission("sd:order:view")]
    [HttpPost("calculate-price")]
    public async Task<ActionResult<ApiResponse<CalculatePriceResponse>>> CalculatePrice([FromBody] CalculatePriceRequest request, CancellationToken cancellationToken)
    {
        return Ok(ApiResponse<CalculatePriceResponse>.Ok(await _pricing.CalculateAsync(request, cancellationToken)));
    }

    [RequirePermission("sd:pricelist:view")]
    [HttpGet("{priceListId:int}/versions")]
    public async Task<ActionResult<ApiResponse<List<PriceListVersionDto>>>> GetVersions(int priceListId, CancellationToken cancellationToken)
    {
        var result = await _pricing.GetVersionsAsync(priceListId, cancellationToken);
        return Ok(ApiResponse<List<PriceListVersionDto>>.Ok(result));
    }

    [RequirePermission("sd:pricelist:view")]
    [HttpGet("{priceListId:int}/versions/{versionId:int}")]
    public async Task<ActionResult<ApiResponse<PriceListVersionDto>>> GetVersion(int priceListId, int versionId, CancellationToken cancellationToken)
    {
        var result = await _pricing.GetVersionAsync(priceListId, versionId, cancellationToken);
        return Ok(ApiResponse<PriceListVersionDto>.Ok(result));
    }

    [RequirePermission("sd:pricelist:view")]
    [HttpGet("{priceListId:int}/versions/{versionId:int}/items")]
    public async Task<ActionResult<ApiResponse<List<PriceListItemDto>>>> GetVersionItems(int priceListId, int versionId, CancellationToken cancellationToken)
    {
        var result = await _pricing.GetVersionAsync(priceListId, versionId, cancellationToken);
        return Ok(ApiResponse<List<PriceListItemDto>>.Ok(result.Items));
    }

    [RequirePermission("sd:pricelist:edit")]
    [HttpPost("{priceListId:int}/versions/clone")]
    public async Task<ActionResult<ApiResponse<PriceListVersionDto>>> CloneVersion(int priceListId, [FromBody] ClonePriceListVersionRequest request, CancellationToken cancellationToken)
    {
        var result = await _pricing.CloneVersionAsync(priceListId, request, cancellationToken);
        return Ok(ApiResponse<PriceListVersionDto>.Ok(result));
    }

    [RequirePermission("sd:pricelist:edit")]
    [HttpPost("{priceListId:int}/versions/{versionId:int}/bulk-adjustment")]
    public async Task<ActionResult<ApiResponse<BulkPriceAdjustmentResponse>>> BulkAdjustment(int priceListId, int versionId, [FromBody] BulkPriceAdjustmentRequest request, CancellationToken cancellationToken)
    {
        var result = await _pricing.BulkAdjustVersionAsync(priceListId, versionId, request, cancellationToken);
        return Ok(ApiResponse<BulkPriceAdjustmentResponse>.Ok(result));
    }
}
