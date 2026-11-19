using ERPm.API.Controllers.Base;
using ERPm.API.Security;
using ERPm.Application.Common;
using ERPm.Application.Common.Crud;
using ERPm.Application.DTOs.Sales;
using ERPm.Domain.Entities.Sales;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

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
    [RequirePermission("sd:pricelist:view", "pur:pricelist:view")]
    [HttpGet]
    public Task<ActionResult<ApiResponse<PagedResult<PriceListDetailDto>>>> Search([FromQuery] PriceListFilterQuery query, CancellationToken cancellationToken) =>
        base.GetPaged(query, cancellationToken);

    [RequirePermission("sd:pricelist:view", "pur:pricelist:view")]
    [HttpGet("{id}")]
    public override Task<ActionResult<ApiResponse<PriceListDetailDto>>> GetById(int id, CancellationToken cancellationToken) =>
        base.GetById(id, cancellationToken);

    [RequirePermission("sd:pricelist:create", "pur:pricelist:create")]
    [HttpPost]
    public override Task<ActionResult<ApiResponse<PriceListDetailDto>>> Create([FromBody] CreatePriceListRequest dto, CancellationToken cancellationToken) =>
        base.Create(dto, cancellationToken);

    [RequirePermission("sd:pricelist:edit", "pur:pricelist:edit")]
    [HttpPut("{id}")]
    public override Task<ActionResult<ApiResponse<PriceListDetailDto>>> Update(int id, [FromBody] UpdatePriceListRequest dto, CancellationToken cancellationToken) =>
        base.Update(id, dto, cancellationToken);

    [RequirePermission("sd:pricelist:delete", "pur:pricelist:delete")]
    [HttpDelete("{id}")]
    public override Task<ActionResult<ApiResponse<bool>>> Delete(int id, CancellationToken cancellationToken) =>
        base.Delete(id, cancellationToken);

    [RequirePermission("sd:order:view")]
    [HttpPost("calculate-price")]
    public async Task<ActionResult<ApiResponse<CalculatePriceResponse>>> CalculatePrice([FromBody] CalculatePriceRequest request, CancellationToken cancellationToken)
    {
        return Ok(ApiResponse<CalculatePriceResponse>.Ok(await _pricing.CalculateAsync(request, cancellationToken)));
    }

    [RequirePermission("sd:pricelist:view", "pur:pricelist:view")]
    [HttpGet("{priceListId:int}/versions")]
    public async Task<ActionResult<ApiResponse<List<PriceListVersionDto>>>> GetVersions(int priceListId, CancellationToken cancellationToken)
    {
        var result = await _pricing.GetVersionsAsync(priceListId, cancellationToken);
        return Ok(ApiResponse<List<PriceListVersionDto>>.Ok(result));
    }

    [RequirePermission("sd:pricelist:view", "pur:pricelist:view")]
    [HttpGet("{priceListId:int}/versions/{versionId:int}")]
    public async Task<ActionResult<ApiResponse<PriceListVersionDto>>> GetVersion(int priceListId, int versionId, CancellationToken cancellationToken)
    {
        var result = await _pricing.GetVersionAsync(priceListId, versionId, cancellationToken);
        return Ok(ApiResponse<PriceListVersionDto>.Ok(result));
    }

    [RequirePermission("sd:pricelist:view", "pur:pricelist:view")]
    [HttpGet("{priceListId:int}/versions/{versionId:int}/items")]
    public async Task<ActionResult<ApiResponse<List<PriceListItemDto>>>> GetVersionItems(int priceListId, int versionId, CancellationToken cancellationToken)
    {
        var result = await _pricing.GetVersionAsync(priceListId, versionId, cancellationToken);
        return Ok(ApiResponse<List<PriceListItemDto>>.Ok(result.Items));
    }

    [RequirePermission("sd:pricelist:view", "pur:pricelist:view")]
    [HttpGet("{priceListId:int}/versions/{versionId:int}/audit")]
    public async Task<ActionResult<ApiResponse<List<PriceListVersionAuditDto>>>> GetVersionAudit(int priceListId, int versionId, CancellationToken cancellationToken)
    {
        var result = await _pricing.GetVersionAuditAsync(priceListId, versionId, cancellationToken);
        return Ok(ApiResponse<List<PriceListVersionAuditDto>>.Ok(result));
    }

    [RequirePermission("sd:pricelist:edit", "pur:pricelist:edit")]
    [HttpPost("{priceListId:int}/versions")]
    public async Task<ActionResult<ApiResponse<PriceListVersionDto>>> CreateVersion(int priceListId, [FromBody] CreatePriceListVersionRequest request, CancellationToken cancellationToken)
    {
        var result = await _pricing.CreateVersionAsync(priceListId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<PriceListVersionDto>.Ok(result, "common.saved"));
    }

    [RequirePermission("sd:pricelist:edit", "pur:pricelist:edit")]
    [HttpPut("{priceListId:int}/versions/{versionId:int}")]
    public async Task<ActionResult<ApiResponse<PriceListVersionDto>>> UpdateVersion(int priceListId, int versionId, [FromBody] UpdatePriceListVersionRequest request, CancellationToken cancellationToken)
    {
        var result = await _pricing.UpdateVersionAsync(priceListId, versionId, request, cancellationToken);
        return Ok(ApiResponse<PriceListVersionDto>.Ok(result, "common.saved"));
    }

    [RequirePermission("sd:pricelist:delete", "pur:pricelist:delete")]
    [HttpDelete("{priceListId:int}/versions/{versionId:int}")]
    public async Task<IActionResult> DeleteVersion(int priceListId, int versionId, CancellationToken cancellationToken)
    {
        await _pricing.DeleteVersionAsync(priceListId, versionId, cancellationToken);
        return NoContent();
    }

    [RequirePermission("sd:pricelist:submit", "pur:pricelist:submit")]
    [HttpPost("{priceListId:int}/versions/{versionId:int}/submit")]
    public async Task<ActionResult<ApiResponse<PriceListVersionDto>>> SubmitVersion(int priceListId, int versionId, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PriceListVersionTransitionRequest? request, CancellationToken cancellationToken)
    {
        var result = await _pricing.SubmitVersionAsync(priceListId, versionId, request, cancellationToken);
        return Ok(ApiResponse<PriceListVersionDto>.Ok(result, "common.saved"));
    }

    [RequirePermission("sd:pricelist:withdraw", "pur:pricelist:withdraw")]
    [HttpPost("{priceListId:int}/versions/{versionId:int}/withdraw")]
    public async Task<ActionResult<ApiResponse<PriceListVersionDto>>> WithdrawVersion(int priceListId, int versionId, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PriceListVersionTransitionRequest? request, CancellationToken cancellationToken)
    {
        var result = await _pricing.WithdrawVersionAsync(priceListId, versionId, request, cancellationToken);
        return Ok(ApiResponse<PriceListVersionDto>.Ok(result, "common.saved"));
    }

    [RequirePermission("sd:pricelist:approve", "pur:pricelist:approve")]
    [HttpPost("{priceListId:int}/versions/{versionId:int}/approve")]
    public async Task<ActionResult<ApiResponse<PriceListVersionDto>>> ApproveVersion(int priceListId, int versionId, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PriceListVersionTransitionRequest? request, CancellationToken cancellationToken)
    {
        var result = await _pricing.ApproveVersionAsync(priceListId, versionId, request, cancellationToken);
        return Ok(ApiResponse<PriceListVersionDto>.Ok(result, "common.saved"));
    }

    [RequirePermission("sd:pricelist:reject", "pur:pricelist:reject")]
    [HttpPost("{priceListId:int}/versions/{versionId:int}/reject")]
    public async Task<ActionResult<ApiResponse<PriceListVersionDto>>> RejectVersion(int priceListId, int versionId, [FromBody] PriceListVersionTransitionRequest request, CancellationToken cancellationToken)
    {
        var result = await _pricing.RejectVersionAsync(priceListId, versionId, request, cancellationToken);
        return Ok(ApiResponse<PriceListVersionDto>.Ok(result, "common.saved"));
    }

    [RequirePermission("sd:pricelist:activate", "pur:pricelist:activate")]
    [HttpPost("{priceListId:int}/versions/{versionId:int}/activate")]
    public async Task<ActionResult<ApiResponse<PriceListVersionDto>>> ActivateVersion(int priceListId, int versionId, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PriceListVersionTransitionRequest? request, CancellationToken cancellationToken)
    {
        var result = await _pricing.ActivateVersionAsync(priceListId, versionId, request, cancellationToken);
        return Ok(ApiResponse<PriceListVersionDto>.Ok(result, "common.saved"));
    }

    [RequirePermission("sd:pricelist:close", "pur:pricelist:close")]
    [HttpPost("{priceListId:int}/versions/{versionId:int}/close")]
    public async Task<ActionResult<ApiResponse<PriceListVersionDto>>> CloseVersion(int priceListId, int versionId, [FromBody] PriceListVersionTransitionRequest request, CancellationToken cancellationToken)
    {
        var result = await _pricing.CloseVersionAsync(priceListId, versionId, request, cancellationToken);
        return Ok(ApiResponse<PriceListVersionDto>.Ok(result, "common.saved"));
    }

    [RequirePermission("sd:pricelist:archive", "pur:pricelist:archive")]
    [HttpPost("{priceListId:int}/versions/{versionId:int}/archive")]
    public async Task<ActionResult<ApiResponse<PriceListVersionDto>>> ArchiveVersion(int priceListId, int versionId, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PriceListVersionTransitionRequest? request, CancellationToken cancellationToken)
    {
        var result = await _pricing.ArchiveVersionAsync(priceListId, versionId, request, cancellationToken);
        return Ok(ApiResponse<PriceListVersionDto>.Ok(result, "common.saved"));
    }

    [RequirePermission("sd:pricelist:edit", "pur:pricelist:edit")]
    [HttpPost("{priceListId:int}/versions/{versionId:int}/items")]
    public async Task<ActionResult<ApiResponse<PriceListItemDto>>> AddVersionItem(int priceListId, int versionId, [FromBody] CreatePriceListItemRequest request, CancellationToken cancellationToken)
    {
        var result = await _pricing.AddVersionItemAsync(priceListId, versionId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<PriceListItemDto>.Ok(result, "common.saved"));
    }

    [RequirePermission("sd:pricelist:edit", "pur:pricelist:edit")]
    [HttpPut("{priceListId:int}/versions/{versionId:int}/items/{itemId:int}")]
    public async Task<ActionResult<ApiResponse<PriceListItemDto>>> UpdateVersionItem(int priceListId, int versionId, int itemId, [FromBody] UpdatePriceListItemRequest request, CancellationToken cancellationToken)
    {
        var result = await _pricing.UpdateVersionItemAsync(priceListId, versionId, itemId, request, cancellationToken);
        return Ok(ApiResponse<PriceListItemDto>.Ok(result, "common.saved"));
    }

    [RequirePermission("sd:pricelist:edit", "pur:pricelist:edit")]
    [HttpDelete("{priceListId:int}/versions/{versionId:int}/items/{itemId:int}")]
    public async Task<IActionResult> DeleteVersionItem(int priceListId, int versionId, int itemId, CancellationToken cancellationToken)
    {
        await _pricing.DeleteVersionItemAsync(priceListId, versionId, itemId, cancellationToken);
        return NoContent();
    }

    [RequirePermission("sd:pricelist:create", "sd:pricelist:edit", "pur:pricelist:create", "pur:pricelist:edit")]
    [HttpPost("{priceListId:int}/versions/clone")]
    public async Task<ActionResult<ApiResponse<PriceListVersionDto>>> CloneVersion(int priceListId, [FromBody] ClonePriceListVersionRequest request, CancellationToken cancellationToken)
    {
        var result = await _pricing.CloneVersionAsync(priceListId, request, cancellationToken);
        return Ok(ApiResponse<PriceListVersionDto>.Ok(result));
    }

    [RequirePermission("sd:pricelist:edit", "pur:pricelist:edit")]
    [HttpPost("{priceListId:int}/versions/{versionId:int}/bulk-adjustment")]
    public async Task<ActionResult<ApiResponse<BulkPriceAdjustmentResponse>>> BulkAdjustment(int priceListId, int versionId, [FromBody] BulkPriceAdjustmentRequest request, CancellationToken cancellationToken)
    {
        var result = await _pricing.BulkAdjustVersionAsync(priceListId, versionId, request, cancellationToken);
        return Ok(ApiResponse<BulkPriceAdjustmentResponse>.Ok(result));
    }
}
