using ERPm.Domain.Common;
using ERPm.Domain.Entities.Accounting;
using ERPm.Domain.Entities.Inventory;
using ERPm.Domain.Entities.Sales;

namespace ERPm.Domain.Entities.System;

public class Branch : BaseAuditableEntity
{
    public string Code { get; set; } = string.Empty;       // e.g. "root", "CN_HN", "CN_HCM"
    public string Name { get; set; } = string.Empty;       // Branch display name
    public int? ParentId { get; set; }                     // Hierarchical parent branch ID
    public int? CompanyId { get; set; }                    // Owning legal entity (optional until configured)
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsDefault { get; set; } = false;           // Default head office branch
    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual Branch? Parent { get; set; }
    public virtual Company? Company { get; set; }
    public virtual ICollection<Branch> Children { get; set; } = new List<Branch>();
    public virtual ICollection<Warehouse> Warehouses { get; set; } = new List<Warehouse>();
    public virtual ICollection<SalesInvoice> SalesInvoices { get; set; } = new List<SalesInvoice>();
    public virtual ICollection<StockReceipt> StockReceipts { get; set; } = new List<StockReceipt>();
    public virtual ICollection<StockIssue> StockIssues { get; set; } = new List<StockIssue>();
    public virtual ICollection<JournalEntry> JournalEntries { get; set; } = new List<JournalEntry>();
}
