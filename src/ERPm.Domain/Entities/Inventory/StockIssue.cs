using ERPm.Domain.Common;
using ERPm.Domain.Entities.Accounting;
using ERPm.Domain.Entities.Catalog;
using ERPm.Domain.Entities.System;
using PartnerEntity = ERPm.Domain.Entities.Partner.Partner;

namespace ERPm.Domain.Entities.Inventory;

public class StockIssue : BaseAuditableEntity
{
    public string Code { get; set; } = string.Empty;       // PXK0001
    public DateTime IssueDate { get; set; } = DateTime.UtcNow;
    public Guid BranchId { get; set; }                     // Chi nhánh xuất kho
    public Guid WarehouseId { get; set; }
    public Guid? CustomerId { get; set; }

    public string? Reason { get; set; }                    // Xuất bán hàng, Xuất hao hụt...
    public decimal TotalCostAmount { get; set; } = 0m;     // Tổng giá trị xuất kho (Giá vốn)
    public VoucherStatus Status { get; set; } = VoucherStatus.Posted;
    public string? Notes { get; set; }

    // Navigation
    public virtual Branch? Branch { get; set; }
    public virtual Warehouse? Warehouse { get; set; }
    public virtual PartnerEntity? Customer { get; set; }
    public virtual ICollection<StockIssueItem> Items { get; set; } = new List<StockIssueItem>();
}

public class StockIssueItem : BaseEntity<Guid>
{
    public StockIssueItem()
    {
        Id = Guid.NewGuid();
    }

    public Guid StockIssueId { get; set; }
    public Guid ItemId { get; set; }

    public decimal Quantity { get; set; } = 0m;
    public decimal UnitCost { get; set; } = 0m;
    public decimal TotalCost { get; set; } = 0m;

    // Navigation
    public virtual StockIssue? StockIssue { get; set; }
    public virtual Item? Item { get; set; }
}
