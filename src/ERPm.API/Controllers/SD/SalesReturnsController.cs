using ERPm.API.Security;
using ERPm.Application.Common;
using ERPm.Application.Common.Crud;
using ERPm.Application.DTOs.Sales;
using ERPm.Domain.Entities.Sales;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers.SALES;

/// <summary>
/// Sales returns (SD module). A return is posted when it is created and cannot be edited or deleted afterwards.
/// </summary>
[PermissionResource("sd:return")]
[Route("api/v1/sales/returns")]
public class SalesReturnsController : BaseApiController
{
    private readonly IBaseCrudService<SalesReturn, SalesReturnDetailDto, CreateSalesReturnRequest, CreateSalesReturnRequest> _service;

    public SalesReturnsController(IBaseCrudService<SalesReturn, SalesReturnDetailDto, CreateSalesReturnRequest, CreateSalesReturnRequest> service)
    {
        _service = service;
    }

    /// <summary>Paged list of returns (keyword on return number or customer, status filter); lines are left out.</summary>
    [RequirePermission(":view")]
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResult<SalesReturnDetailDto>>>> GetPaged([FromQuery] BaseFilterQuery query, CancellationToken cancellationToken) =>
        Ok(ApiResponse<PagedResult<SalesReturnDetailDto>>.Ok(await _service.GetPagedAsync(query, cancellationToken)));

    /// <summary>Return with its lines.</summary>
    [RequirePermission(":view")]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<SalesReturnDetailDto>>> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return result == null ? NotFound(ApiResponse<SalesReturnDetailDto>.Fail("error.validation.reference")) : Ok(ApiResponse<SalesReturnDetailDto>.Ok(result));
    }

    /// <summary>Posts a return: stock receipt of the goods, revenue and cost reversal, refund.</summary>
    [RequirePermission(":create")]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<SalesReturnDetailDto>>> Create([FromBody] CreateSalesReturnRequest request, CancellationToken cancellationToken) =>
        Ok(ApiResponse<SalesReturnDetailDto>.Ok(await _service.CreateAsync(request, cancellationToken), "common.saved"));
}
