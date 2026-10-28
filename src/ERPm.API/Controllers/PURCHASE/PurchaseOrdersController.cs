using ERPm.API.Controllers.Base;
using ERPm.Application.Common;
using ERPm.Application.Common.Crud;
using ERPm.Application.DTOs.Purchase;
using ERPm.Domain.Entities.Purchase;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers.PURCHASE;

/// <summary>
/// Quản lý Đơn đặt mua hàng từ Nhà cung cấp (PURCHASE Module)
/// Kế thừa BaseCrudController: Tự động có trọn bộ CRUD Paged, GetById, Create, Update, Delete.
/// </summary>
[Route("api/v1/purchase/orders")]
public class PurchaseOrdersController : BaseCrudController<PurchaseOrder, PurchaseOrderDto, CreatePurchaseOrderRequest, UpdatePurchaseOrderRequest>
{
    public PurchaseOrdersController(IBaseCrudService<PurchaseOrder, PurchaseOrderDto, CreatePurchaseOrderRequest, UpdatePurchaseOrderRequest> service)
        : base(service)
    {
    }

    /// <summary>
    /// Ví dụ mở rộng Endpoint nghiệp vụ: Xác nhận đơn mua hàng
    /// </summary>
    [HttpPost("{id:guid}/confirm")]
    public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> ConfirmOrder(Guid id, CancellationToken cancellationToken)
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
