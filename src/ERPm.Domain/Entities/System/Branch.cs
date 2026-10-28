using ERPm.Domain.Common;
using ERPm.Domain.Entities.Accounting;
using ERPm.Domain.Entities.Inventory;
using ERPm.Domain.Entities.Sales;

namespace ERPm.Domain.Entities.System;

public class Branch : BaseAuditableEntity
{
    public string Code { get; set; } = string.Empty;       // "root", "CN_HN", "CN_HCM"
    public string Name { get; set; } = string.Empty;       // "Tổng Công Ty", "Chi nhánh Hà Nội"
    public Guid? ParentId { get; set; }                    // Phân cấp chi nhánh (hierarchical)
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsDefault { get; set; } = false;           // Chi nhánh mặc định (VD: root)
    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual Branch? Parent { get; set; }
    public virtual ICollection<Branch> Children { get; set; } = new List<Branch>();
    public virtual ICollection<Warehouse> Warehouses { get; set; } = new List<Warehouse>();
    public virtual ICollection<SalesInvoice> SalesInvoices { get; set; } = new List<SalesInvoice>();
    public virtual ICollection<StockReceipt> StockReceipts { get; set; } = new List<StockReceipt>();
    public virtual ICollection<StockIssue> StockIssues { get; set; } = new List<StockIssue>();
    public virtual ICollection<JournalEntry> JournalEntries { get; set; } = new List<JournalEntry>();
}
