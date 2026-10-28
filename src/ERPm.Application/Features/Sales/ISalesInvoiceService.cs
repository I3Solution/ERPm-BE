using ERPm.Application.DTOs.Sales;

namespace ERPm.Application.Features.Sales;

public interface ISalesInvoiceService
{
    Task<SalesInvoiceDto> CreateInvoiceAsync(CreateSalesInvoiceRequest request, CancellationToken cancellationToken = default);
    Task<List<SalesInvoiceDto>> GetInvoicesAsync(Guid? branchId = null, CancellationToken cancellationToken = default);
}
