using ERPm.API.Security;
using ERPm.API.Controllers.Base;
using ERPm.Application.Common;
using ERPm.Application.Common.Crud;
using ERPm.Application.DTOs.Purchase;
using ERPm.Domain.Entities.Purchase;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers.PURCHASE;

/// <summary>
/// Manages supplier purchase orders (PURCHASE Module).
/// Inherits BaseCrudController for full type-safe CRUD endpoints.
/// </summary>
[PermissionResource("pur:order")]
[Route("api/v1/purchase/orders")]
public class PurchaseOrdersController : BaseCrudController<PurchaseOrder, PurchaseOrderDto, CreatePurchaseOrderRequest, UpdatePurchaseOrderRequest>
{
    private readonly IOrderWorkflow<PurchaseOrderDto> _workflow;
    public PurchaseOrdersController(IBaseCrudService<PurchaseOrder, PurchaseOrderDto, CreatePurchaseOrderRequest, UpdatePurchaseOrderRequest> service, IOrderWorkflow<PurchaseOrderDto> workflow)
        : base(service)
    { _workflow = workflow; }

    /// <summary>
    /// Custom domain action: Confirms a purchase order by its internal integer ID.
    /// </summary>
    [RequirePermission(":confirm")]
    [HttpPost("{id:int}/confirm")]
    public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> ConfirmOrder(int id, CancellationToken cancellationToken)
    {
        return Ok(ApiResponse<PurchaseOrderDto>.Ok(await _workflow.ConfirmAsync(id, cancellationToken)));
    }
}
