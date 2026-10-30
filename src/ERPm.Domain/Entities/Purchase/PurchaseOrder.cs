using ERPm.Domain.Common;
using ERPm.Domain.Entities.Catalog;
using ERPm.Domain.Entities.System;
using PartnerEntity = ERPm.Domain.Entities.Partner.Partner;

namespace ERPm.Domain.Entities.Purchase;

public class PurchaseOrder : BaseAuditableEntity
{
    public string OrderNumber { get; set; } = string.Empty; // e.g. PO0001
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    public int BranchId { get; set; }                      // Purchasing branch ID
    public int? DepartmentId { get; set; }                 // Owning department, used by DEPARTMENT data scopes
    public int? SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;

    // Monetary fields in decimal(18, 4)
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

public class PurchaseOrderItem : BaseEntity
{
    public int PurchaseOrderId { get; set; }
    public int ItemId { get; set; }
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
