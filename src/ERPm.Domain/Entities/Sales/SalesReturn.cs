using ERPm.Domain.Common;
using ERPm.Domain.Entities.Accounting;
using ERPm.Domain.Entities.Catalog;
using ERPm.Domain.Entities.Inventory;
using ERPm.Domain.Entities.System;
using PartnerEntity = ERPm.Domain.Entities.Partner.Partner;

namespace ERPm.Domain.Entities.Sales;

/// <summary>How the customer gets the money of returned goods back.</summary>
public enum RefundMethod
{
    Cash = 1,           // Paid out of cash on hand (Account 111)
    BankTransfer = 2,   // Paid by bank transfer (Account 112)
    ReduceDebt = 3      // Credit memo against the customer's receivable (Account 131)
}

/// <summary>
/// Goods returned by a customer. Posting a return puts the goods back into stock, reverses revenue and cost of
/// sales and refunds the customer. It is immutable once posted, like an invoice.
/// </summary>
public class SalesReturn : BaseAuditableEntity
{
    public string ReturnNumber { get; set; } = string.Empty;      // SR-YYYYMM-XXXX
    public DateTime ReturnDate { get; set; } = DateTime.UtcNow;

    public int? InvoiceId { get; set; }                           // Original invoice; null for a return without invoice
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;

    public int BranchId { get; set; }
    public int? DepartmentId { get; set; }                        // Owning department, used by DEPARTMENT data scopes
    public int WarehouseId { get; set; }                          // Warehouse receiving the goods

    public RefundMethod RefundMethod { get; set; } = RefundMethod.Cash;
    public VoucherStatus Status { get; set; } = VoucherStatus.Posted;
    public decimal TotalRefundAmount { get; set; } = 0m;          // decimal(18, 4)
    public decimal TotalCostAmount { get; set; } = 0m;            // Cost put back into inventory, decimal(18, 4)

    public int? StockReceiptId { get; set; }                      // Receipt of the returned goods
    public int? JournalEntryId { get; set; }                      // Revenue / cost reversal
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }

    // Navigation
    public virtual SalesInvoice? Invoice { get; set; }
    public virtual PartnerEntity? Customer { get; set; }
    public virtual Branch? Branch { get; set; }
    public virtual Warehouse? Warehouse { get; set; }
    public virtual StockReceipt? StockReceipt { get; set; }
    public virtual JournalEntry? JournalEntry { get; set; }
    public virtual ICollection<SalesReturnItem> Items { get; set; } = new List<SalesReturnItem>();
}

public class SalesReturnItem : BaseEntity
{
    public int SalesReturnId { get; set; }
    public int? InvoiceItemId { get; set; }
    public int ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? UnitName { get; set; }

    public decimal ReturnQuantity { get; set; } = 0m;
    public decimal UnitPrice { get; set; } = 0m;                  // Net refund price per unit
    public decimal RefundAmount { get; set; } = 0m;               // ReturnQuantity * UnitPrice
    public decimal CostAmount { get; set; } = 0m;                 // Cost of the goods put back, 0 for services
    public string? Reason { get; set; }

    // Navigation
    public virtual SalesReturn? SalesReturn { get; set; }
    public virtual SalesInvoiceItem? InvoiceItem { get; set; }
    public virtual Item? Item { get; set; }
}
