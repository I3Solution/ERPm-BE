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
[Route("api/v1/sales/orders")]
public class SalesOrdersController : BaseCrudController<SalesOrder, SalesOrderDto, CreateSalesOrderRequest, UpdateSalesOrderRequest>
{
    private readonly IOrderWorkflow<SalesOrderDto> _workflow;
    public SalesOrdersController(IBaseCrudService<SalesOrder, SalesOrderDto, CreateSalesOrderRequest, UpdateSalesOrderRequest> service, IOrderWorkflow<SalesOrderDto> workflow)
        : base(service)
    { _workflow = workflow; }

    /// <summary>
    /// Custom domain action: Confirms a sales order by its internal integer ID.
    /// </summary>
    [HttpPost("{id:int}/confirm")]
    public async Task<ActionResult<ApiResponse<SalesOrderDto>>> ConfirmOrder(int id, CancellationToken cancellationToken)
    {
        return Ok(ApiResponse<SalesOrderDto>.Ok(await _workflow.ConfirmAsync(id, cancellationToken)));
    }
}
