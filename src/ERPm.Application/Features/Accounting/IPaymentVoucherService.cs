using ERPm.Application.DTOs.Accounting;

namespace ERPm.Application.Features.Accounting;

public interface IPaymentVoucherService
{
    Task<List<PaymentVoucherDto>> GetPaymentVouchersAsync(int? branchId = null, int? partnerId = null, CancellationToken ct = default);
    Task<PaymentVoucherDto?> GetPaymentVoucherAsync(int id, CancellationToken ct = default);
    Task<PaymentVoucherDto> CreatePaymentVoucherAsync(CreatePaymentVoucherRequest request, CancellationToken ct = default);
    Task<PaymentVoucherDto> ReversePaymentVoucherAsync(int id, ReverseVoucherRequest? request = null, CancellationToken ct = default);
}
