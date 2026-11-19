namespace ERPm.Application.Common.Crud;

/// <summary>Order actions that follow the credit check and stock reservation of a confirmed order.</summary>
public interface IOrderApproval<TDto>
{
    /// <summary>Approves an order held back by the credit limit; confirms it and reserves stock.</summary>
    Task<TDto> ApproveCreditAsync(int id, string reason, CancellationToken cancellationToken);

    /// <summary>Rejects an order held back by the credit limit and returns it to Draft; it holds no stock.</summary>
    Task<TDto> RejectCreditAsync(int id, string reason, CancellationToken cancellationToken);

    /// <summary>Status change history of an order, oldest first.</summary>
    Task<List<ERPm.Application.DTOs.Sales.SalesOrderStatusAuditDto>> GetStatusHistoryAsync(int id, CancellationToken cancellationToken);

    /// <summary>Cancels an order that is not invoiced yet and releases its reserved stock.</summary>
    Task<TDto> CancelAsync(int id, string reason, CancellationToken cancellationToken);
}
