using ERPm.API.Security;
using ERPm.Application.Common;
using ERPm.Application.DTOs.Accounting;
using ERPm.Application.Features.Accounting;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers.ACC;

[Route("api/v1/accounting/payment-vouchers")]
public class PaymentVouchersController(IPaymentVoucherService paymentService) : BaseApiController
{
    [RequirePermission("acc:payment-voucher:view")]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<PaymentVoucherDto>>>> GetAll(
        [FromQuery] int? branchId,
        [FromQuery] int? partnerId,
        CancellationToken ct)
    {
        var result = await paymentService.GetPaymentVouchersAsync(branchId, partnerId, ct);
        return Ok(ApiResponse<List<PaymentVoucherDto>>.Ok(result));
    }

    [RequirePermission("acc:payment-voucher:view")]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<PaymentVoucherDto>>> GetById(int id, CancellationToken ct)
    {
        var result = await paymentService.GetPaymentVoucherAsync(id, ct);
        return result == null ? NotFound() : Ok(ApiResponse<PaymentVoucherDto>.Ok(result));
    }

    [RequirePermission("acc:payment-voucher:create")]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<PaymentVoucherDto>>> Create(
        [FromBody] CreatePaymentVoucherRequest request,
        CancellationToken ct)
    {
        var result = await paymentService.CreatePaymentVoucherAsync(request, ct);
        return Ok(ApiResponse<PaymentVoucherDto>.Ok(result, "common.created"));
    }

    [RequirePermission("acc:payment-voucher:cancel")]
    [HttpPost("{id:int}/reverse")]
    public async Task<ActionResult<ApiResponse<PaymentVoucherDto>>> Reverse(
        int id,
        [FromBody] ReverseVoucherRequest? request,
        CancellationToken ct)
    {
        var result = await paymentService.ReversePaymentVoucherAsync(id, request, ct);
        return Ok(ApiResponse<PaymentVoucherDto>.Ok(result, "common.updated"));
    }
}
