using ERPm.Application.DTOs.Sales;

namespace ERPm.Application.Common.Crud;

/// <summary>Status changes of a sales quotation beyond plain CRUD.</summary>
public interface IQuotationWorkflow
{
    /// <summary>Draft to Sent.</summary>
    Task<SalesQuotationDetailDto> SendAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Sent to Accepted (the customer agreed).</summary>
    Task<SalesQuotationDetailDto> AcceptAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Sent or Accepted to Rejected.</summary>
    Task<SalesQuotationDetailDto> RejectAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Creates one Draft sales order from a live quotation and marks the quotation Converted.</summary>
    Task<ConvertQuotationResponse> ConvertToOrderAsync(int id, CancellationToken cancellationToken = default);
}
