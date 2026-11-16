using ERPm.Application.Common;
using ERPm.Application.DTOs.Accounting;

namespace ERPm.Application.Features.Accounting;

public interface IPaymentVoucherService
{
    Task<PagedResult<PaymentVoucherDto>> GetPaymentVouchersAsync(
        int? branchId = null,
        int? partnerId = null,
        string? q = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);
    Task<PaymentVoucherDto?> GetPaymentVoucherAsync(int id, CancellationToken ct = default);
    Task<PaymentVoucherDto> CreatePaymentVoucherAsync(CreatePaymentVoucherRequest request, CancellationToken ct = default);
    Task<PaymentVoucherDto> ReversePaymentVoucherAsync(int id, ReverseVoucherRequest? request = null, CancellationToken ct = default);
}
