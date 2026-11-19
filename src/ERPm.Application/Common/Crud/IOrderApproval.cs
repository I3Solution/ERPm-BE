namespace ERPm.Application.Common.Crud;

/// <summary>Order actions that follow the credit check and stock reservation of a confirmed order.</summary>
public interface IOrderApproval<TDto>
{
    /// <summary>Approves an order held back by the credit limit; confirms it and reserves stock.</summary>
    Task<TDto> ApproveCreditAsync(int id, string reason, CancellationToken cancellationToken);

    /// <summary>Rejects an order held back by the credit limit and returns it to Draft; it holds no stock.</summary>
    Task<TDto> RejectCreditAsync(int id, string reason, CancellationToken cancellationToken);

    /// <summary>
    /// Runs the single credit approval (approve = true) or rejection on each order in turn, each in its own transaction,
    /// so one failed order does not undo the others. Returns one result per order, in request order.
    /// </summary>
    Task<List<ERPm.Application.DTOs.Sales.BulkOrderActionResultDto>> DecideCreditManyAsync(IReadOnlyList<int> ids, string reason, bool approve, CancellationToken cancellationToken);

    /// <summary>Status change history of an order, oldest first.</summary>
    Task<List<ERPm.Application.DTOs.Sales.SalesOrderStatusAuditDto>> GetStatusHistoryAsync(int id, CancellationToken cancellationToken);

    /// <summary>Cancels an order that is not invoiced yet and releases its reserved stock.</summary>
    Task<TDto> CancelAsync(int id, string reason, CancellationToken cancellationToken);
}
