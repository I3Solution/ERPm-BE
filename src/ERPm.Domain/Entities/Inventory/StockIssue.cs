using ERPm.Domain.Common;
using ERPm.Domain.Entities.Accounting;
using ERPm.Domain.Entities.Catalog;
using ERPm.Domain.Entities.System;
using PartnerEntity = ERPm.Domain.Entities.Partner.Partner;

namespace ERPm.Domain.Entities.Inventory;

public class StockIssue : BaseAuditableEntity
{
    public string Code { get; set; } = string.Empty;       // e.g. PXK0001
    public DateTime IssueDate { get; set; } = DateTime.UtcNow;
    public int BranchId { get; set; }                      // Issuing branch ID
    public int? DepartmentId { get; set; }                 // Owning department, used by DEPARTMENT data scopes
    public int WarehouseId { get; set; }
    public int? CustomerId { get; set; }

    public string? Reason { get; set; }                    // Issue reason (e.g. Sales, Adjustment)
    public decimal TotalCostAmount { get; set; } = 0m;     // Total COGS amount
    public VoucherStatus Status { get; set; } = VoucherStatus.Posted;
    public string? Notes { get; set; }

    // Navigation
    public virtual Branch? Branch { get; set; }
    public virtual Warehouse? Warehouse { get; set; }
    public virtual PartnerEntity? Customer { get; set; }
    public virtual ICollection<StockIssueItem> Items { get; set; } = new List<StockIssueItem>();
}

public class StockIssueItem : BaseEntity
{
    public int StockIssueId { get; set; }
    public int ItemId { get; set; }

    public decimal Quantity { get; set; } = 0m;
    public decimal UnitCost { get; set; } = 0m;
    public decimal TotalCost { get; set; } = 0m;

    // Navigation
    public virtual StockIssue? StockIssue { get; set; }
    public virtual Item? Item { get; set; }
}
