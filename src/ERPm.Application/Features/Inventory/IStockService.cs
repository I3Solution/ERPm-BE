using ERPm.Application.DTOs.Inventory;

namespace ERPm.Application.Features.Inventory;

public interface IStockService
{
    Task<List<WarehouseDto>> GetWarehousesAsync(int? branchId = null, CancellationToken cancellationToken = default);
    Task<List<StockBalanceDto>> GetStockBalancesAsync(int? warehouseId, int? branchId = null, CancellationToken cancellationToken = default);
    Task<bool> CreateStockReceiptAsync(CreateStockReceiptRequest request, CancellationToken cancellationToken = default);
}
