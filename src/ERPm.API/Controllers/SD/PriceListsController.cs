using ERPm.API.Controllers.Base;
using ERPm.API.Security;
using ERPm.Application.Common;
using ERPm.Application.Common.Crud;
using ERPm.Application.DTOs.Sales;
using ERPm.Domain.Entities.Sales;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers.SALES;

/// <summary>
/// Selling price lists (SD module) and the automatic price calculation used when lines are added to quotations and orders.
/// </summary>
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

    /// <summary>
    /// Works out the best unit price, discount and line total of each requested item for a customer and branch.
    /// Needs only order-view rights because every seller must be able to price a line.
    /// </summary>
    [RequirePermission("sd:order:view")]
    [HttpPost("calculate-price")]
    public async Task<ActionResult<ApiResponse<CalculatePriceResponse>>> CalculatePrice([FromBody] CalculatePriceRequest request, CancellationToken cancellationToken)
    {
        return Ok(ApiResponse<CalculatePriceResponse>.Ok(await _pricing.CalculateAsync(request, cancellationToken)));
    }
}
