using ERPm.Domain.Common;
using ERPm.Domain.Entities.System;

namespace ERPm.Domain.Entities.Accounting;

public class JournalEntry : BaseAuditableEntity
{
    public int? ReversesJournalEntryId { get; set; }
    public string VoucherNumber { get; set; } = string.Empty; // Voucher number (e.g. PKT00001, HD00001)
    public string VoucherType { get; set; } = string.Empty;   // SALES_INVOICE, STOCK_ISSUE, PAYMENT, RECEIPT
    public DateTime DocDate { get; set; } = DateTime.UtcNow;  // Document date
    public DateTime PostingDate { get; set; } = DateTime.UtcNow; // Ledger posting date

    public int BranchId { get; set; }                         // Originating branch ID
    public string? Description { get; set; }                  // General journal description
    public VoucherStatus Status { get; set; } = VoucherStatus.Draft;

    public int? ReferenceId { get; set; }                     // Source document ID (e.g. SalesInvoiceId)

    // Navigation
    public virtual Branch? Branch { get; set; }

    // Double-entry detail lines (Debit / Credit)
    public virtual ICollection<JournalEntryLine> Lines { get; set; } = new List<JournalEntryLine>();
}
