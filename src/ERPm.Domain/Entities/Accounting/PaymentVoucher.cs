using ERPm.Domain.Common;
using ERPm.Domain.Entities.System;
using PartnerEntity = ERPm.Domain.Entities.Partner.Partner;

namespace ERPm.Domain.Entities.Accounting;

public class PaymentVoucher : BaseAuditableEntity
{
    public string VoucherNumber { get; set; } = string.Empty;
    public DateTime VoucherDate { get; set; } = DateTime.UtcNow;

    public int BranchId { get; set; }
    public TreasuryPaymentMethod PaymentMethod { get; set; } = TreasuryPaymentMethod.Cash;
    public int? CashFundId { get; set; }
    public int? BankAccountId { get; set; }

    public int? PartnerId { get; set; }
    public string RecipientName { get; set; } = string.Empty;

    public decimal Amount { get; set; } = 0m;
    public PaymentType PaymentType { get; set; } = PaymentType.SupplierPayment;
    public int? ExpenseAccountId { get; set; }
    public VoucherStatus Status { get; set; } = VoucherStatus.Draft;

    public int? JournalEntryId { get; set; }
    public string? Notes { get; set; }

    // Navigation
    public virtual Branch? Branch { get; set; }
    public virtual CashFund? CashFund { get; set; }
    public virtual BankAccount? BankAccount { get; set; }
    public virtual PartnerEntity? Partner { get; set; }
    public virtual Account? ExpenseAccount { get; set; }
    public virtual JournalEntry? JournalEntry { get; set; }
}
