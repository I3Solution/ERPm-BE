using ERPm.Application.DTOs.System;
using ERPm.Application.Features.Inventory;
using ERPm.Application.Features.System;

namespace ERPm.Infrastructure.Services;

/// <summary>Lets the Branch Settings page list the effective serving warehouse policy; WMS owns its writes and editor.</summary>
public sealed class ServingWarehousePolicyProvider(IStockService stock) : IBranchSettingModuleProvider
{
    public string Key => StockService.PolicyKey;

    public Task<SettingViewDto> GetAsync(int branchId, CancellationToken ct = default) => stock.GetServingPolicyAsync(branchId, ct);
}
