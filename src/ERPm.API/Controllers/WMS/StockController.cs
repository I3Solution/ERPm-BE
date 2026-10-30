using ERPm.API.Security;
using ERPm.Application.Common;
using ERPm.Application.DTOs.Inventory;
using ERPm.Application.Features.Inventory;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers;

public class StockController : BaseApiController
{
    private readonly IStockService _stockService;

    public StockController(IStockService stockService)
    {
        _stockService = stockService;
    }

    [RequirePermission("wms:stock:view")]
    [HttpGet("warehouses")]
    public async Task<ActionResult<ApiResponse<List<WarehouseDto>>>> GetWarehouses([FromQuery] int? branchId, CancellationToken cancellationToken)
    {
        var result = await _stockService.GetWarehousesAsync(branchId, cancellationToken);
        return Ok(ApiResponse<List<WarehouseDto>>.Ok(result));
    }

    [RequirePermission("wms:stock:view")]
    [HttpGet("balances")]
    public async Task<ActionResult<ApiResponse<List<StockBalanceDto>>>> GetStockBalances([FromQuery] int? warehouseId, [FromQuery] int? branchId, CancellationToken cancellationToken)
    {
        var result = await _stockService.GetStockBalancesAsync(warehouseId, branchId, cancellationToken);
        return Ok(ApiResponse<List<StockBalanceDto>>.Ok(result));
    }

    [RequirePermission("wms:receipt:create")]
    [HttpPost("receipts")]
    public async Task<ActionResult<ApiResponse<bool>>> CreateStockReceipt([FromBody] CreateStockReceiptRequest request, CancellationToken cancellationToken)
    {
        var success = await _stockService.CreateStockReceiptAsync(request, cancellationToken);
        if (!success)
        {
            return BadRequest(ApiResponse<bool>.Fail("error.validation.reference"));
        }
        return Ok(ApiResponse<bool>.Ok(true, "common.saved"));
    }
}
