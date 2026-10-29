using ERPm.Domain.Common;
using ERPm.Domain.Entities.Accounting;
using ERPm.Domain.Entities.Catalog;
using ERPm.Domain.Entities.System;
using PartnerEntity = ERPm.Domain.Entities.Partner.Partner;

namespace ERPm.Domain.Entities.Inventory;

public class StockReceipt : BaseAuditableEntity
{
    public Guid? RequestId { get; set; }
    public string? RequestHash { get; set; }
    public int? JournalEntryId { get; set; }
    public virtual JournalEntry? JournalEntry { get; set; }
    public string Code { get; set; } = string.Empty;       // e.g. PNK0001
    public DateTime ReceiptDate { get; set; } = DateTime.UtcNow;
    public int BranchId { get; set; }                      // Receiving branch ID
    public int WarehouseId { get; set; }
    public int? SupplierId { get; set; }                   // Supplier partner ID

    public decimal TotalAmount { get; set; } = 0m;         // Total receipt value
    public VoucherStatus Status { get; set; } = VoucherStatus.Posted;
    public string? Notes { get; set; }

    // Navigation
    public virtual Branch? Branch { get; set; }
    public virtual Warehouse? Warehouse { get; set; }
    public virtual PartnerEntity? Supplier { get; set; }
    public virtual ICollection<StockReceiptItem> Items { get; set; } = new List<StockReceiptItem>();
}

public class StockReceiptItem : BaseEntity
{
    public int StockReceiptId { get; set; }
    public int ItemId { get; set; }

    public decimal Quantity { get; set; } = 0m;            // Received quantity
    public decimal UnitPrice { get; set; } = 0m;           // Unit receipt price
    public decimal Amount { get; set; } = 0m;              // Line total amount

    // Navigation
    public virtual StockReceipt? StockReceipt { get; set; }
    public virtual Item? Item { get; set; }
}
