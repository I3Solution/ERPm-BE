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
[Route("api/v1/purchase/orders")]
public class PurchaseOrdersController : BaseCrudController<PurchaseOrder, PurchaseOrderDto, CreatePurchaseOrderRequest, UpdatePurchaseOrderRequest>
{
    public PurchaseOrdersController(IBaseCrudService<PurchaseOrder, PurchaseOrderDto, CreatePurchaseOrderRequest, UpdatePurchaseOrderRequest> service)
        : base(service)
    {
    }

    /// <summary>
    /// Custom domain action: Confirms a purchase order by its internal integer ID.
    /// </summary>
    [HttpPost("{id:int}/confirm")]
    public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> ConfirmOrder(int id, CancellationToken cancellationToken)
    {
        var existing = await Service.GetByIdAsync(id, cancellationToken);
        if (existing == null)
        {
            return NotFound(ApiResponse<PurchaseOrderDto>.Fail($"Đơn mua với ID '{id}' không tồn tại."));
        }

        var updateReq = new UpdatePurchaseOrderRequest
        {
            BranchId = existing.BranchId,
            SupplierId = existing.SupplierId,
            SupplierName = existing.SupplierName,
            TaxAmount = existing.TaxAmount,
            PaidAmount = existing.PaidAmount,
            Notes = existing.Notes,
            Status = "Confirmed"
        };

        var updated = await Service.UpdateAsync(id, updateReq, cancellationToken);
        return Ok(ApiResponse<PurchaseOrderDto>.Ok(updated!, "Xác nhận đơn mua hàng thành công."));
    }
}
