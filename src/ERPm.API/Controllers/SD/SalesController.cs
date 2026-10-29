using ERPm.Application.Common;
using ERPm.Application.DTOs.Sales;
using ERPm.Application.Features.Sales;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers;

public class SalesController : BaseApiController
{
    private readonly ISalesInvoiceService _salesService;

    public SalesController(ISalesInvoiceService salesService)
    {
        _salesService = salesService;
    }

    [HttpGet("invoices")]
    public async Task<ActionResult<ApiResponse<List<SalesInvoiceDto>>>> GetInvoices([FromQuery] int? branchId, CancellationToken cancellationToken)
    {
        var result = await _salesService.GetInvoicesAsync(branchId, cancellationToken);
        return Ok(ApiResponse<List<SalesInvoiceDto>>.Ok(result));
    }

    [HttpPost("pos-checkout")]
    public async Task<ActionResult<ApiResponse<SalesInvoiceDto>>> Checkout([FromBody] CreateSalesInvoiceRequest request, CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0)
        {
            return BadRequest(ApiResponse<SalesInvoiceDto>.Fail("Invoice must contain at least one item."));
        }

        var result = await _salesService.CreateInvoiceAsync(request, cancellationToken);
        return Ok(ApiResponse<SalesInvoiceDto>.Ok(result, "Invoice created, stock deducted, and double-entry ledger posted successfully."));
    }
}
