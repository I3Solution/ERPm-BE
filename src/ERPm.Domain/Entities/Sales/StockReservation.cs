using ERPm.Domain.Common;
using ERPm.Domain.Entities.Catalog;
using ERPm.Domain.Entities.Inventory;

namespace ERPm.Domain.Entities.Sales;

public static class ReservationRecordStatus
{
    public const string Active = "Active";
    public const string Released = "Released";
}

/// <summary>
/// Quantity of one order line held back in one warehouse so it cannot be sold elsewhere.
/// The sum of active rows of a warehouse and item equals <see cref="StockBalance.QuantityReserved"/>.
/// </summary>
public class StockReservation : BaseEntity
{
    public int SalesOrderId { get; set; }
    public int SalesOrderItemId { get; set; }
    public int WarehouseId { get; set; }
    public int ItemId { get; set; }
    public decimal Quantity { get; set; } = 0m;                    // decimal(18, 4)
    public string Status { get; set; } = ReservationRecordStatus.Active;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReleasedAtUtc { get; set; }

    // Navigation
    public virtual SalesOrder? SalesOrder { get; set; }
    public virtual SalesOrderItem? SalesOrderItem { get; set; }
    public virtual Warehouse? Warehouse { get; set; }
    public virtual Item? Item { get; set; }
}
