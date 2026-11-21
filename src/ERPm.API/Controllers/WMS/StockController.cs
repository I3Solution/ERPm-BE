using ERPm.API.Security;
using ERPm.Application.Common;
using ERPm.Application.DTOs.Inventory;
using ERPm.Application.DTOs.System;
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
    /// <c>includePolicy=true</c> it returns the SettingView of key wms.so_warehouse_policy with its ETag
    /// (also needs sys:branch-setting:view on the unit).
    /// </summary>
    [RequirePermission("wms:stock:view")]
    [HttpGet("branches/{branchId:int}/serving-warehouses")]
    public async Task<IActionResult> GetServingWarehouses(int branchId, [FromQuery] bool includePolicy, CancellationToken cancellationToken)
        => includePolicy
            ? WithEtag(await _stockService.GetServingPolicyAsync(branchId, cancellationToken))
            : Ok(ApiResponse<List<ServingWarehouseDto>>.Ok(await _stockService.GetServingWarehousesAsync(branchId, cancellationToken)));

    /// <summary>What a save would change for the unit and the units inheriting from it, and what blocks it. Needs If-Match.</summary>
    [RequirePermission("sys:branch:edit")]
    [HttpPost("branches/{branchId:int}/serving-warehouses/preview")]
    public async Task<ActionResult<ApiResponse<SettingImpactDto>>> PreviewServingWarehouses(int branchId, [FromBody] UpdateServingWarehousesRequest request, CancellationToken cancellationToken)
        => Ok(ApiResponse<SettingImpactDto>.Ok(await _stockService.PreviewServingPolicyAsync(branchId, request, Request.Headers.IfMatch.ToString(), cancellationToken)));

    /// <summary>
    /// With a mode (fixed_list, own_warehouses, inherit), a reason and If-Match it saves the policy and returns the
    /// SettingView. Without a mode (legacy) a list with rows is a fixed list and an empty list the branch's own
    /// warehouses; it returns the array. Both also need sys:branch-setting:edit on every affected unit.
    /// </summary>
    [RequirePermission("sys:branch:edit")]
    [HttpPut("branches/{branchId:int}/serving-warehouses")]
    public async Task<IActionResult> UpdateServingWarehouses(int branchId, [FromBody] UpdateServingWarehousesRequest request, CancellationToken cancellationToken)
        => request.Mode == null
            ? Ok(ApiResponse<List<ServingWarehouseDto>>.Ok(await _stockService.UpdateServingWarehousesAsync(branchId, request, cancellationToken)))
            : WithEtag(await _stockService.SaveServingPolicyAsync(branchId, request, Request.Headers.IfMatch.ToString(), cancellationToken), "common.saved");

    private OkObjectResult WithEtag(SettingViewDto view, string? message = null)
    {
        Response.Headers.ETag = view.Etag;
        return Ok(ApiResponse<SettingViewDto>.Ok(view, message));
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
