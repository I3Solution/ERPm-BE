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
    public async Task<ActionResult<ApiResponse<List<StockBalanceDto>>>> GetStockBalances([FromQuery] int? warehouseId, [FromQuery] int? branchId, [FromQuery] ERPm.Application.Common.Crud.ListSort listSort, CancellationToken cancellationToken)
    {
        var result = await _stockService.GetStockBalancesAsync(warehouseId, branchId, listSort, cancellationToken);
        return Ok(ApiResponse<List<StockBalanceDto>>.Ok(result));
    }

    /// <summary>
    /// Warehouses a selling branch ships its sales orders from (its own when it has no list). With
    /// <c>includePolicy=true</c> it returns the Branch Settings view of key wms.so_warehouse_policy instead.
    /// </summary>
    [RequirePermission("wms:stock:view")]
    [HttpGet("branches/{branchId:int}/serving-warehouses")]
    public async Task<IActionResult> GetServingWarehouses(int branchId, [FromQuery] bool includePolicy, CancellationToken cancellationToken)
        => includePolicy
            ? Ok(ApiResponse<ServingPolicyView>.Ok(await _stockService.GetServingPolicyAsync(branchId, CanEditAt(branchId), cancellationToken)))
            : Ok(ApiResponse<List<ServingWarehouseDto>>.Ok(await _stockService.GetServingWarehousesAsync(branchId, cancellationToken)));

    /// <summary>What a save would change for the unit and the units inheriting from it, and what blocks it.</summary>
    [RequirePermission("sys:branch:edit")]
    [HttpPost("branches/{branchId:int}/serving-warehouses/preview")]
    public async Task<ActionResult<ApiResponse<ServingPolicyPreview>>> PreviewServingWarehouses(int branchId, [FromBody] UpdateServingWarehousesRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<ServingPolicyPreview>.Ok(await _stockService.PreviewServingPolicyAsync(branchId, request, cancellationToken)));

    /// <summary>
    /// With a mode (fixed_list, own_warehouses, inherit) and a reason it saves the policy and returns the view. Without a
    /// mode (legacy) a list with rows is a fixed list and an empty list the branch's own warehouses; it returns the array.
    /// </summary>
    [RequirePermission("sys:branch:edit")]
    [HttpPut("branches/{branchId:int}/serving-warehouses")]
    public async Task<IActionResult> UpdateServingWarehouses(int branchId, [FromBody] UpdateServingWarehousesRequest request, CancellationToken cancellationToken)
        => request.Mode == null
            ? Ok(ApiResponse<List<ServingWarehouseDto>>.Ok(await _stockService.UpdateServingWarehousesAsync(branchId, request, cancellationToken)))
            : Ok(ApiResponse<ServingPolicyView>.Ok(await _stockService.SaveServingPolicyAsync(branchId, request, cancellationToken)));

    private bool CanEditAt(int branchId)
        => HttpContext.Principal()?.Evaluate("sys:branch:edit") is { Allowed: true } decision && decision.Scope.CoversBranch(branchId);

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
