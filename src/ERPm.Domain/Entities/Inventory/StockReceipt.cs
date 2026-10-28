using ERPm.Domain.Common;
using ERPm.Domain.Entities.Accounting;
using ERPm.Domain.Entities.Catalog;
using ERPm.Domain.Entities.System;
using PartnerEntity = ERPm.Domain.Entities.Partner.Partner;

namespace ERPm.Domain.Entities.Inventory;

public class StockReceipt : BaseAuditableEntity
{
    public string Code { get; set; } = string.Empty;       // PNK0001
    public DateTime ReceiptDate { get; set; } = DateTime.UtcNow;
    public Guid BranchId { get; set; }                     // Chi nhánh nhập kho
    public Guid WarehouseId { get; set; }
    public Guid? SupplierId { get; set; }                  // Nhà cung cấp

    public decimal TotalAmount { get; set; } = 0m;         // Tổng giá trị nhập kho
    public VoucherStatus Status { get; set; } = VoucherStatus.Posted;
    public string? Notes { get; set; }

    // Navigation
    public virtual Branch? Branch { get; set; }
    public virtual Warehouse? Warehouse { get; set; }
    public virtual PartnerEntity? Supplier { get; set; }
    public virtual ICollection<StockReceiptItem> Items { get; set; } = new List<StockReceiptItem>();
}

public class StockReceiptItem : BaseEntity<Guid>
{
    public StockReceiptItem()
    {
        Id = Guid.NewGuid();
    }

    public Guid StockReceiptId { get; set; }
    public Guid ItemId { get; set; }

    public decimal Quantity { get; set; } = 0m;            // Số lượng nhập
    public decimal UnitPrice { get; set; } = 0m;           // Đơn giá nhập
    public decimal Amount { get; set; } = 0m;              // Thành tiền

    // Navigation
    public virtual StockReceipt? StockReceipt { get; set; }
    public virtual Item? Item { get; set; }
}
