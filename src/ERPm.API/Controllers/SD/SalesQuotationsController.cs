using ERPm.API.Controllers.Base;
using ERPm.API.Security;
using ERPm.Application.Common;
using ERPm.Application.Common.Crud;
using ERPm.Application.DTOs.Sales;
using ERPm.Domain.Entities.Sales;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers.SALES;

/// <summary>
/// Customer price quotations (SD module): CRUD, status changes and one-click conversion into a sales order.
/// </summary>
[PermissionResource("sd:quotation")]
[Route("api/v1/sales/quotations")]
public class SalesQuotationsController : BaseCrudController<SalesQuotation, SalesQuotationDetailDto, CreateSalesQuotationRequest, UpdateSalesQuotationRequest>
{
    private readonly IQuotationWorkflow _workflow;

    public SalesQuotationsController(IBaseCrudService<SalesQuotation, SalesQuotationDetailDto, CreateSalesQuotationRequest, UpdateSalesQuotationRequest> service, IQuotationWorkflow workflow)
        : base(service)
    {
        _workflow = workflow;
    }

    /// <summary>Marks a Draft quotation as sent to the customer.</summary>
    [RequirePermission(":edit")]
    [HttpPost("{id:int}/send")]
    public async Task<ActionResult<ApiResponse<SalesQuotationDetailDto>>> Send(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<SalesQuotationDetailDto>.Ok(await _workflow.SendAsync(id, cancellationToken)));

    /// <summary>Records that the customer accepted a sent quotation.</summary>
    [RequirePermission(":edit")]
    [HttpPost("{id:int}/accept")]
    public async Task<ActionResult<ApiResponse<SalesQuotationDetailDto>>> Accept(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<SalesQuotationDetailDto>.Ok(await _workflow.AcceptAsync(id, cancellationToken)));

    /// <summary>Records that the customer rejected a sent or accepted quotation.</summary>
    [RequirePermission(":edit")]
    [HttpPost("{id:int}/reject")]
    public async Task<ActionResult<ApiResponse<SalesQuotationDetailDto>>> Reject(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<SalesQuotationDetailDto>.Ok(await _workflow.RejectAsync(id, cancellationToken)));

    /// <summary>Creates a Draft sales order from the quotation. Needs the right to create orders, not to edit quotations.</summary>
    [RequirePermission("sd:order:create")]
    [HttpPost("{id:int}/convert-to-order")]
    public async Task<ActionResult<ApiResponse<ConvertQuotationResponse>>> ConvertToOrder(int id, CancellationToken cancellationToken) =>
        Ok(ApiResponse<ConvertQuotationResponse>.Ok(await _workflow.ConvertToOrderAsync(id, cancellationToken), "common.saved"));
}
