using ERPm.Domain.Common;
using ERPm.Domain.Entities.Catalog;

namespace ERPm.Domain.Entities.Inventory;

public class StockBalance : BaseAuditableEntity
{
    public Guid WarehouseId { get; set; }
    public Guid ItemId { get; set; }

    // Số lượng tồn & Đơn giá vốn bình quân (Bắt buộc decimal(18, 4))
    public decimal QuantityOnHand { get; set; } = 0m;
    public decimal AverageCostPrice { get; set; } = 0m;

    // Navigation
    public virtual Warehouse? Warehouse { get; set; }
    public virtual Item? Item { get; set; }
}
