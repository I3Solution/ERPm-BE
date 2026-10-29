using ERPm.Domain.Common;
using ERPm.Domain.Entities.Catalog;
using ERPm.Domain.Entities.System;
using PartnerEntity = ERPm.Domain.Entities.Partner.Partner;

namespace ERPm.Domain.Entities.Sales;

public class SalesOrder : BaseAuditableEntity
{
    public string OrderNumber { get; set; } = string.Empty; // e.g. SO0001
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    public int BranchId { get; set; }                      // Ordering branch ID
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = "Khách lẻ";

    // Monetary fields in decimal(18, 4)
    public decimal SubTotal { get; set; } = 0m;
    public decimal DiscountAmount { get; set; } = 0m;
    public decimal TaxAmount { get; set; } = 0m;
    public decimal TotalAmount { get; set; } = 0m;
    public decimal PaidAmount { get; set; } = 0m;

    public string Status { get; set; } = "Draft";          // Draft, Confirmed, Completed, Cancelled
    public string? Notes { get; set; }

    // Navigation
    public virtual Branch? Branch { get; set; }
    public virtual PartnerEntity? Customer { get; set; }
    public virtual ICollection<SalesOrderItem> Items { get; set; } = new List<SalesOrderItem>();
}

public class SalesOrderItem : BaseEntity
{
    public int SalesOrderId { get; set; }
    public int ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? UnitName { get; set; }

    public decimal Quantity { get; set; } = 0m;
    public decimal UnitPrice { get; set; } = 0m;
    public decimal DiscountAmount { get; set; } = 0m;
    public decimal LineTotal { get; set; } = 0m;

    // Navigation
    public virtual SalesOrder? SalesOrder { get; set; }
    public virtual Item? Item { get; set; }
}
