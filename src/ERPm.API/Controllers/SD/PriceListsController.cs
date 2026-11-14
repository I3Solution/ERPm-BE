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

    /// <summary>Hidden: replaced by <see cref="Search"/>, which binds the extended filter.</summary>
    [NonAction]
    public override Task<ActionResult<ApiResponse<PagedResult<PriceListDetailDto>>>> GetPaged(BaseFilterQuery query, CancellationToken cancellationToken) =>
        base.GetPaged(query, cancellationToken);

    /// <summary>Paged list with standard filters plus optional ?type=Sales|Purchase (or 1|2).</summary>
    [RequirePermission(":view")]
    [HttpGet]
    public Task<ActionResult<ApiResponse<PagedResult<PriceListDetailDto>>>> Search([FromQuery] PriceListFilterQuery query, CancellationToken cancellationToken) =>
        base.GetPaged(query, cancellationToken);

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
    [HttpPost("{priceListId:int}/versions")]
    public async Task<ActionResult<ApiResponse<PriceListVersionDto>>> CreateVersion(int priceListId, [FromBody] CreatePriceListVersionRequest request, CancellationToken cancellationToken)
    {
        var result = await _pricing.CreateVersionAsync(priceListId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<PriceListVersionDto>.Ok(result, "common.saved"));
    }

    [RequirePermission("sd:pricelist:edit")]
    [HttpPut("{priceListId:int}/versions/{versionId:int}")]
    public async Task<ActionResult<ApiResponse<PriceListVersionDto>>> UpdateVersion(int priceListId, int versionId, [FromBody] UpdatePriceListVersionRequest request, CancellationToken cancellationToken)
    {
        var result = await _pricing.UpdateVersionAsync(priceListId, versionId, request, cancellationToken);
        return Ok(ApiResponse<PriceListVersionDto>.Ok(result, "common.saved"));
    }

    [RequirePermission("sd:pricelist:edit")]
    [HttpPost("{priceListId:int}/versions/{versionId:int}/activate")]
    public async Task<ActionResult<ApiResponse<PriceListVersionDto>>> ActivateVersion(int priceListId, int versionId, CancellationToken cancellationToken)
    {
        var result = await _pricing.ActivateVersionAsync(priceListId, versionId, cancellationToken);
        return Ok(ApiResponse<PriceListVersionDto>.Ok(result, "common.saved"));
    }

    [RequirePermission("sd:pricelist:edit")]
    [HttpPost("{priceListId:int}/versions/{versionId:int}/close")]
    public async Task<ActionResult<ApiResponse<PriceListVersionDto>>> CloseVersion(int priceListId, int versionId, CancellationToken cancellationToken)
    {
        var result = await _pricing.CloseVersionAsync(priceListId, versionId, cancellationToken);
        return Ok(ApiResponse<PriceListVersionDto>.Ok(result, "common.saved"));
    }

    [RequirePermission("sd:pricelist:edit")]
    [HttpPost("{priceListId:int}/versions/{versionId:int}/items")]
    public async Task<ActionResult<ApiResponse<PriceListItemDto>>> AddVersionItem(int priceListId, int versionId, [FromBody] CreatePriceListItemRequest request, CancellationToken cancellationToken)
    {
        var result = await _pricing.AddVersionItemAsync(priceListId, versionId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<PriceListItemDto>.Ok(result, "common.saved"));
    }

    [RequirePermission("sd:pricelist:edit")]
    [HttpPut("{priceListId:int}/versions/{versionId:int}/items/{itemId:int}")]
    public async Task<ActionResult<ApiResponse<PriceListItemDto>>> UpdateVersionItem(int priceListId, int versionId, int itemId, [FromBody] UpdatePriceListItemRequest request, CancellationToken cancellationToken)
    {
        var result = await _pricing.UpdateVersionItemAsync(priceListId, versionId, itemId, request, cancellationToken);
        return Ok(ApiResponse<PriceListItemDto>.Ok(result, "common.saved"));
    }

    [RequirePermission("sd:pricelist:edit")]
    [HttpDelete("{priceListId:int}/versions/{versionId:int}/items/{itemId:int}")]
    public async Task<IActionResult> DeleteVersionItem(int priceListId, int versionId, int itemId, CancellationToken cancellationToken)
    {
        await _pricing.DeleteVersionItemAsync(priceListId, versionId, itemId, cancellationToken);
        return NoContent();
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
