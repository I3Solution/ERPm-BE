using ERPm.API.Security;
using ERPm.Application.Common;
using ERPm.Application.DTOs.Sales;
using ERPm.Application.Features.Sales;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers;

public class SalesController : BaseApiController
{
    public sealed record CancelRequest(string Reason);

    [RequirePermission("sd:invoice:cancel")]
    [HttpPost("invoices/{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancelRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<SalesInvoiceDto>.Ok(await _salesService.CancelInvoiceAsync(id, request.Reason, cancellationToken)));
    private readonly ISalesInvoiceService _salesService;

    public SalesController(ISalesInvoiceService salesService)
    {
        _salesService = salesService;
    }

    [RequirePermission("sd:invoice:view")]
    [HttpGet("invoices")]
    public async Task<ActionResult<ApiResponse<List<SalesInvoiceDto>>>> GetInvoices([FromQuery] int? branchId, CancellationToken cancellationToken)
    {
        var result = await _salesService.GetInvoicesAsync(branchId, cancellationToken);
        return Ok(ApiResponse<List<SalesInvoiceDto>>.Ok(result));
    }

    [RequirePermission("sd:invoice:create")]
    [HttpPost("pos-checkout")]
    public async Task<ActionResult<ApiResponse<SalesInvoiceDto>>> Checkout([FromBody] CreateSalesInvoiceRequest request, CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
        {
            return BadRequest(ApiResponse<SalesInvoiceDto>.Fail("error.validation.reference"));
        }

        var result = await _salesService.CreateInvoiceAsync(request, cancellationToken);
        return Ok(ApiResponse<SalesInvoiceDto>.Ok(result, "common.saved"));
    }
}
