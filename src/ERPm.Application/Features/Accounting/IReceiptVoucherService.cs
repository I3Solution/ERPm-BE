using ERPm.Application.DTOs.Accounting;

namespace ERPm.Application.Features.Accounting;

public interface IReceiptVoucherService
{
    Task<List<ReceiptVoucherDto>> GetReceiptVouchersAsync(int? branchId = null, int? partnerId = null, CancellationToken ct = default);
    Task<ReceiptVoucherDto?> GetReceiptVoucherAsync(int id, CancellationToken ct = default);
    Task<ReceiptVoucherDto> CreateReceiptVoucherAsync(CreateReceiptVoucherRequest request, CancellationToken ct = default);
    Task<ReceiptVoucherDto> ReverseReceiptVoucherAsync(int id, ReverseVoucherRequest? request = null, CancellationToken ct = default);
}
