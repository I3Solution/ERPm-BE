using ERPm.Domain.Common;
using ERPm.Domain.Entities.Sales;
using ERPm.Domain.Entities.System;
using PartnerEntity = ERPm.Domain.Entities.Partner.Partner;

namespace ERPm.Domain.Entities.Accounting;

public class ReceiptVoucher : BaseAuditableEntity
{
    public string VoucherNumber { get; set; } = string.Empty;
    public DateTime VoucherDate { get; set; } = DateTime.UtcNow;

    public int BranchId { get; set; }
    public TreasuryPaymentMethod PaymentMethod { get; set; } = TreasuryPaymentMethod.Cash;
    public int? CashFundId { get; set; }
    public int? BankAccountId { get; set; }

    public int? PartnerId { get; set; }
    public string PayerName { get; set; } = string.Empty;
    public string? PayerPhone { get; set; }

    public decimal Amount { get; set; } = 0m;
    public ReceiptType ReceiptType { get; set; } = ReceiptType.CustomerPayment;
    public VoucherStatus Status { get; set; } = VoucherStatus.Draft;

    public int? JournalEntryId { get; set; }
    public string? Notes { get; set; }

    // Navigation
    public virtual Branch? Branch { get; set; }
    public virtual CashFund? CashFund { get; set; }
    public virtual BankAccount? BankAccount { get; set; }
    public virtual PartnerEntity? Partner { get; set; }
    public virtual JournalEntry? JournalEntry { get; set; }
    public virtual ICollection<ReceiptVoucherAllocation> Allocations { get; set; } = new List<ReceiptVoucherAllocation>();
}

public class ReceiptVoucherAllocation : BaseEntity
{
    public int ReceiptVoucherId { get; set; }
    public int SalesInvoiceId { get; set; }
    public decimal AllocatedAmount { get; set; } = 0m;

    // Navigation
    public virtual ReceiptVoucher? ReceiptVoucher { get; set; }
    public virtual SalesInvoice? SalesInvoice { get; set; }
}
