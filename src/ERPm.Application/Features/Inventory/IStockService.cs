using ERPm.Application.DTOs.Inventory;

namespace ERPm.Application.Features.Inventory;

public interface IStockService
{
    Task<List<WarehouseDto>> GetWarehousesAsync(int? branchId = null, CancellationToken cancellationToken = default);
    Task<List<StockBalanceDto>> GetStockBalancesAsync(int? warehouseId, int? branchId = null, ERPm.Application.Common.Crud.ListSort? sort = null, CancellationToken cancellationToken = default);
    Task<bool> CreateStockReceiptAsync(CreateStockReceiptRequest request, CancellationToken cancellationToken = default);
    Task<List<ServingWarehouseDto>> GetServingWarehousesAsync(int branchId, CancellationToken cancellationToken = default);
    Task<List<ServingWarehouseDto>> UpdateServingWarehousesAsync(int branchId, UpdateServingWarehousesRequest request, CancellationToken cancellationToken = default);
    /// <summary>Branch Settings view of the unit's serving warehouse policy (key wms.so_warehouse_policy).</summary>
    Task<ServingPolicyView> GetServingPolicyAsync(int branchId, bool canEdit, CancellationToken cancellationToken = default);
    /// <summary>What saving the request would change for the unit and the units inheriting from it, with blockers.</summary>
    Task<ServingPolicyPreview> PreviewServingPolicyAsync(int branchId, UpdateServingWarehousesRequest request, CancellationToken cancellationToken = default);
    /// <summary>Saves a request with an explicit mode and returns the new view.</summary>
    Task<ServingPolicyView> SaveServingPolicyAsync(int branchId, UpdateServingWarehousesRequest request, CancellationToken cancellationToken = default);
    /// <summary>Availability of items in a warehouse the selling branch may ship from.</summary>
    Task<List<StockAvailabilityDto>> GetAvailabilityAsync(int branchId, int warehouseId, IReadOnlyCollection<int> itemIds, CancellationToken cancellationToken = default);
}
