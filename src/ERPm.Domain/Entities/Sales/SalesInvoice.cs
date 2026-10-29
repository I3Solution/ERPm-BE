using ERPm.Domain.Common;
using ERPm.Domain.Entities.Accounting;
using ERPm.Domain.Entities.Catalog;
using ERPm.Domain.Entities.Inventory;
using ERPm.Domain.Entities.System;
using PartnerEntity = ERPm.Domain.Entities.Partner.Partner;

namespace ERPm.Domain.Entities.Sales;

public enum PaymentMethod
{
    Cash = 1,           // Cash on hand (Account 111)
    BankTransfer = 2,   // Bank transfer (Account 112)
    Debt = 3            // Accounts receivable (Account 131)
}

public class SalesInvoice : BaseAuditableEntity
{
    public string InvoiceNumber { get; set; } = string.Empty; // e.g. HD0001
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;

    public int BranchId { get; set; }                      // Issuing branch ID
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = "Khách lẻ";
    public int WarehouseId { get; set; }

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public VoucherStatus Status { get; set; } = VoucherStatus.Posted;

    // Monetary fields (Required decimal(18, 4))
    public decimal SubTotal { get; set; } = 0m;            // Gross subtotal before discount
    public decimal DiscountAmount { get; set; } = 0m;      // Invoice discount amount
    public decimal TaxAmount { get; set; } = 0m;           // VAT amount
    public decimal TotalAmount { get; set; } = 0m;         // Net total payable
    public decimal PaidAmount { get; set; } = 0m;          // Amount paid by customer
    public decimal RemainingDebt { get; set; } = 0m;       // Remaining receivable (Account 131)

    // Linked accounting journal entry & stock issue
    public int? JournalEntryId { get; set; }               // Corresponding double-entry journal ID
    public int? StockIssueId { get; set; }                 // Corresponding stock issue ID
    public string? Notes { get; set; }

    // Navigation
    public virtual Branch? Branch { get; set; }
    public virtual PartnerEntity? Customer { get; set; }
    public virtual Warehouse? Warehouse { get; set; }
    public virtual JournalEntry? JournalEntry { get; set; }
    public virtual StockIssue? StockIssue { get; set; }
    public virtual ICollection<SalesInvoiceItem> Items { get; set; } = new List<SalesInvoiceItem>();
}

public class SalesInvoiceItem : BaseEntity
{
    public int SalesInvoiceId { get; set; }
    public int ItemId { get; set; }

    public decimal Quantity { get; set; } = 0m;
    public decimal UnitPrice { get; set; } = 0m;           // Unit selling price
    public decimal DiscountPercent { get; set; } = 0m;
    public decimal Amount { get; set; } = 0m;              // Net line total after discount
    public decimal CostPrice { get; set; } = 0m;           // Unit cost at time of sale for COGS

    // Navigation
    public virtual SalesInvoice? SalesInvoice { get; set; }
    public virtual Item? Item { get; set; }
}
