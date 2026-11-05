using ERPm.API.Security;
using ERPm.API.Controllers.Base;
using ERPm.Application.Common;
using ERPm.Application.Common.Crud;
using ERPm.Application.DTOs.Sales;
using ERPm.Domain.Entities.Sales;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers.SALES;

/// <summary>
/// Manages customer sales orders (SD Module).
/// Inherits BaseCrudController for full type-safe CRUD endpoints.
/// </summary>
[PermissionResource("sd:order")]
[Route("api/v1/sales/orders")]
public class SalesOrdersController : BaseCrudController<SalesOrder, SalesOrderDto, CreateSalesOrderRequest, UpdateSalesOrderRequest>
{
    private readonly IOrderWorkflow<SalesOrderDto> _workflow;
    private readonly IOrderApproval<SalesOrderDto> _approval;
    public SalesOrdersController(IBaseCrudService<SalesOrder, SalesOrderDto, CreateSalesOrderRequest, UpdateSalesOrderRequest> service, IOrderWorkflow<SalesOrderDto> workflow, IOrderApproval<SalesOrderDto> approval)
        : base(service)
    { _workflow = workflow; _approval = approval; }

    /// <summary>
    /// Custom domain action: Confirms a sales order by its internal integer ID.
    /// </summary>
    [RequirePermission(":confirm")]
    [HttpPost("{id:int}/confirm")]
    public async Task<ActionResult<ApiResponse<SalesOrderDto>>> ConfirmOrder(int id, CancellationToken cancellationToken)
    {
        return Ok(ApiResponse<SalesOrderDto>.Ok(await _workflow.ConfirmAsync(id, cancellationToken)));
    }
    /// <summary>
    /// Approves an order that exceeded the customer's credit limit. The order is confirmed and its stock reserved.
    /// </summary>
    [RequirePermission(":approve_credit")]
    [HttpPost("{id:int}/approve-credit")]
    public async Task<ActionResult<ApiResponse<SalesOrderDto>>> ApproveCredit(int id, [FromBody] ApproveCreditRequest request, CancellationToken cancellationToken)
    {
        return Ok(ApiResponse<SalesOrderDto>.Ok(await _approval.ApproveCreditAsync(id, request.Reason, cancellationToken), "common.saved"));
    }

    /// <summary>
    /// Cancels an order that has not been invoiced and releases the stock it reserved.
    /// </summary>
    [RequirePermission(":cancel")]
    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<ApiResponse<SalesOrderDto>>> Cancel(int id, [FromBody] CancelOrderRequest request, CancellationToken cancellationToken)
    {
        return Ok(ApiResponse<SalesOrderDto>.Ok(await _approval.CancelAsync(id, request.Reason, cancellationToken), "common.saved"));
    }
}
