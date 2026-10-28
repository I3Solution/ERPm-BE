using ERPm.Domain.Common;
using ERPm.Domain.Entities.Catalog;
using ERPm.Domain.Entities.System;
using PartnerEntity = ERPm.Domain.Entities.Partner.Partner;

namespace ERPm.Domain.Entities.Purchase;

public class PurchaseOrder : BaseAuditableEntity
{
    public string OrderNumber { get; set; } = string.Empty; // PO0001
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    public Guid BranchId { get; set; }                     // Chi nhánh lập đơn mua
    public Guid? SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;

    // Tiền tệ decimal(18, 4)
    public decimal SubTotal { get; set; } = 0m;
    public decimal TaxAmount { get; set; } = 0m;
    public decimal TotalAmount { get; set; } = 0m;
    public decimal PaidAmount { get; set; } = 0m;

    public string Status { get; set; } = "Draft";          // Draft, Confirmed, Received, Cancelled
    public string? Notes { get; set; }

    // Navigation
    public virtual Branch? Branch { get; set; }
    public virtual PartnerEntity? Supplier { get; set; }
    public virtual ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
}

public class PurchaseOrderItem : BaseEntity<Guid>
{
    public PurchaseOrderItem()
    {
        Id = Guid.NewGuid();
    }

    public Guid PurchaseOrderId { get; set; }
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? UnitName { get; set; }

    public decimal Quantity { get; set; } = 0m;
    public decimal UnitPrice { get; set; } = 0m;
    public decimal LineTotal { get; set; } = 0m;

    // Navigation
    public virtual PurchaseOrder? PurchaseOrder { get; set; }
    public virtual Item? Item { get; set; }
}
