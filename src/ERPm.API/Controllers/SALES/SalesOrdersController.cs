using ERPm.API.Controllers.Base;
using ERPm.Application.Common;
using ERPm.Application.Common.Crud;
using ERPm.Application.DTOs.Sales;
using ERPm.Domain.Entities.Sales;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers.SALES;

/// <summary>
/// Quản lý Đơn đặt hàng bán (SALES Module)
/// Kế thừa BaseCrudController: Tự động có trọn bộ CRUD Paged, GetById, Create, Update, Delete.
/// Đồng thời dễ dàng bổ sung các Action nghiệp vụ mở rộng (như duyệt đơn, hủy đơn).
/// </summary>
[Route("api/v1/sales/orders")]
public class SalesOrdersController : BaseCrudController<SalesOrder, SalesOrderDto, CreateSalesOrderRequest, UpdateSalesOrderRequest>
{
    public SalesOrdersController(IBaseCrudService<SalesOrder, SalesOrderDto, CreateSalesOrderRequest, UpdateSalesOrderRequest> service)
        : base(service)
    {
    }

    /// <summary>
    /// Ví dụ mở rộng Endpoint nghiệp vụ: Xác nhận đơn đặt hàng bán
    /// </summary>
    [HttpPost("{id:guid}/confirm")]
    public async Task<ActionResult<ApiResponse<SalesOrderDto>>> ConfirmOrder(Guid id, CancellationToken cancellationToken)
    {
        var existing = await Service.GetByIdAsync(id, cancellationToken);
        if (existing == null)
        {
            return NotFound(ApiResponse<SalesOrderDto>.Fail($"Đơn hàng với ID '{id}' không tồn tại."));
        }

        var updateReq = new UpdateSalesOrderRequest
        {
            BranchId = existing.BranchId,
            CustomerId = existing.CustomerId,
            CustomerName = existing.CustomerName,
            DiscountAmount = existing.DiscountAmount,
            TaxAmount = existing.TaxAmount,
            PaidAmount = existing.PaidAmount,
            Notes = existing.Notes,
            Status = "Confirmed"
        };

        var updated = await Service.UpdateAsync(id, updateReq, cancellationToken);
        return Ok(ApiResponse<SalesOrderDto>.Ok(updated!, "Xác nhận đơn đặt hàng thành công."));
    }
}
