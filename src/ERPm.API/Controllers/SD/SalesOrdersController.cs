using ERPm.API.Security;
using ERPm.API.Controllers.Base;
using ERPm.Application.Common;
using ERPm.Application.Common.Crud;
using ERPm.Application.DTOs.Inventory;
using ERPm.Application.DTOs.Sales;
using ERPm.Application.Features.Inventory;
using ERPm.Application.Features.Sales;
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
    private readonly ISalesInvoiceService _invoicing;
    private readonly IStockService _stock;
    public SalesOrdersController(IBaseCrudService<SalesOrder, SalesOrderDto, CreateSalesOrderRequest, UpdateSalesOrderRequest> service, IOrderWorkflow<SalesOrderDto> workflow, IOrderApproval<SalesOrderDto> approval, ISalesInvoiceService invoicing, IStockService stock)
        : base(service)
    { _workflow = workflow; _approval = approval; _invoicing = invoicing; _stock = stock; }

    /// <summary>Warehouses the order's branch may ship from, default first.</summary>
    [RequirePermission(":view")]
    [HttpGet("serving-warehouses")]
    public async Task<ActionResult<ApiResponse<List<ServingWarehouseDto>>>> GetServingWarehouses([FromQuery] int branchId, CancellationToken cancellationToken)
        => Ok(ApiResponse<List<ServingWarehouseDto>>.Ok(await _stock.GetServingWarehousesAsync(branchId, cancellationToken)));

    /// <summary>Available stock of the given items in a warehouse serving the branch.</summary>
    [RequirePermission(":view")]
    [HttpGet("availability")]
    public async Task<ActionResult<ApiResponse<List<StockAvailabilityDto>>>> GetAvailability([FromQuery] int branchId, [FromQuery] int warehouseId, [FromQuery] int[] itemIds, CancellationToken cancellationToken)
        => Ok(ApiResponse<List<StockAvailabilityDto>>.Ok(await _stock.GetAvailabilityAsync(branchId, warehouseId, itemIds, cancellationToken)));

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
    /// Rejects an order waiting for credit approval and returns it to Draft. A reason is required.
    /// </summary>
    [RequirePermission(":reject_credit")]
    [HttpPost("{id:int}/reject-credit")]
    public async Task<ActionResult<ApiResponse<SalesOrderDto>>> RejectCredit(int id, [FromBody] ApproveCreditRequest request, CancellationToken cancellationToken)
    {
        return Ok(ApiResponse<SalesOrderDto>.Ok(await _approval.RejectCreditAsync(id, request.Reason, cancellationToken), "common.saved"));
    }

    /// <summary>Who changed the order status, when and why, oldest first.</summary>
    [RequirePermission(":view")]
    [HttpGet("{id:int}/status-history")]
    public async Task<ActionResult<ApiResponse<List<SalesOrderStatusAuditDto>>>> StatusHistory(int id, CancellationToken cancellationToken)
    {
        return Ok(ApiResponse<List<SalesOrderStatusAuditDto>>.Ok(await _approval.GetStatusHistoryAsync(id, cancellationToken)));
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
    /// <summary>
    /// Invoices a confirmed order: releases its held stock, issues the goods and posts the invoice at the order prices.
    /// </summary>
    [RequirePermission("sd:invoice:create")]
    [HttpPost("{id:int}/create-invoice")]
    public async Task<ActionResult<ApiResponse<SalesInvoiceDto>>> CreateInvoice(int id, [FromBody] CreateInvoiceFromOrderRequest request, CancellationToken cancellationToken)
    {
        return Ok(ApiResponse<SalesInvoiceDto>.Ok(await _invoicing.CreateInvoiceFromOrderAsync(id, request, cancellationToken), "common.saved"));
    }
}
