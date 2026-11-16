using ERPm.Application.Common;
using ERPm.Application.DTOs.Accounting;

namespace ERPm.Application.Features.Accounting;

public interface IReceiptVoucherService
{
    Task<PagedResult<ReceiptVoucherDto>> GetReceiptVouchersAsync(
        int? branchId = null,
        int? partnerId = null,
        string? q = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);
    Task<ReceiptVoucherDto?> GetReceiptVoucherAsync(int id, CancellationToken ct = default);
    Task<ReceiptVoucherDto> CreateReceiptVoucherAsync(CreateReceiptVoucherRequest request, CancellationToken ct = default);
    Task<ReceiptVoucherDto> ReverseReceiptVoucherAsync(int id, ReverseVoucherRequest? request = null, CancellationToken ct = default);
}
