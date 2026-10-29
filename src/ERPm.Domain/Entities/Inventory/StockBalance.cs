using ERPm.Domain.Common;
using ERPm.Domain.Entities.Catalog;

namespace ERPm.Domain.Entities.Inventory;

public class StockBalance : BaseAuditableEntity
{
    public int WarehouseId { get; set; }
    public int ItemId { get; set; }

    // On-hand quantity and weighted average cost (Required decimal(18, 4))
    public decimal QuantityOnHand { get; set; } = 0m;
    public decimal AverageCostPrice { get; set; } = 0m;

    // Navigation
    public virtual Warehouse? Warehouse { get; set; }
    public virtual Item? Item { get; set; }
}
