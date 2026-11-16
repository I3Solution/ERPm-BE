using ERPm.API.Security;
using ERPm.Application.Common;
using ERPm.Application.DTOs.Accounting;
using ERPm.Application.Features.Accounting;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers.ACC;

[Route("api/v1/accounting/receipt-vouchers")]
public class ReceiptVouchersController(IReceiptVoucherService receiptService) : BaseApiController
{
    [RequirePermission("acc:receipt-voucher:view")]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ReceiptVoucherDto>>>> GetAll(
        [FromQuery] int? branchId,
        [FromQuery] int? partnerId,
        CancellationToken ct)
    {
        var result = await receiptService.GetReceiptVouchersAsync(branchId, partnerId, ct);
        return Ok(ApiResponse<List<ReceiptVoucherDto>>.Ok(result));
    }

    [RequirePermission("acc:receipt-voucher:view")]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<ReceiptVoucherDto>>> GetById(int id, CancellationToken ct)
    {
        var result = await receiptService.GetReceiptVoucherAsync(id, ct);
        return result == null ? NotFound() : Ok(ApiResponse<ReceiptVoucherDto>.Ok(result));
    }

    [RequirePermission("acc:receipt-voucher:create")]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ReceiptVoucherDto>>> Create(
        [FromBody] CreateReceiptVoucherRequest request,
        CancellationToken ct)
    {
        var result = await receiptService.CreateReceiptVoucherAsync(request, ct);
        return Ok(ApiResponse<ReceiptVoucherDto>.Ok(result, "common.created"));
    }

    [RequirePermission("acc:receipt-voucher:cancel")]
    [HttpPost("{id:int}/reverse")]
    public async Task<ActionResult<ApiResponse<ReceiptVoucherDto>>> Reverse(
        int id,
        [FromBody] ReverseVoucherRequest? request,
        CancellationToken ct)
    {
        var result = await receiptService.ReverseReceiptVoucherAsync(id, request, ct);
        return Ok(ApiResponse<ReceiptVoucherDto>.Ok(result, "common.updated"));
    }
}
