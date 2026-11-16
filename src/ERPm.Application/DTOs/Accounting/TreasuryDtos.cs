using ERPm.Domain.Entities.Accounting;

namespace ERPm.Application.DTOs.Accounting;

public class CashFundDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public string? BranchName { get; set; }
    public int AccountId { get; set; }
    public string? AccountCode { get; set; }
    public int? CustodianEmployeeId { get; set; }
    public decimal CurrentBalance { get; set; }
    public string Currency { get; set; } = "VND";
    public bool IsActive { get; set; }
}

public class CreateCashFundRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public int AccountId { get; set; }
    public int? CustodianEmployeeId { get; set; }
    public decimal OpeningBalance { get; set; } = 0m;
    public string Currency { get; set; } = "VND";
    public bool IsActive { get; set; } = true;
}

public class UpdateCashFundRequest
{
    public string Name { get; set; } = string.Empty;
    public int AccountId { get; set; }
    public int? CustodianEmployeeId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class BankAccountDto
{
    public int Id { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;
    public string? BankBranch { get; set; }
    public int BranchId { get; set; }
    public string? BranchName { get; set; }
    public int AccountId { get; set; }
    public string? AccountCode { get; set; }
    public decimal CurrentBalance { get; set; }
    public string Currency { get; set; } = "VND";
    public bool IsActive { get; set; }
}

public class CreateBankAccountRequest
{
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;
    public string? BankBranch { get; set; }
    public int BranchId { get; set; }
    public int AccountId { get; set; }
    public decimal OpeningBalance { get; set; } = 0m;
    public string Currency { get; set; } = "VND";
    public bool IsActive { get; set; } = true;
}

public class UpdateBankAccountRequest
{
    public string BankName { get; set; } = string.Empty;
    public string AccountHolder { get; set; } = string.Empty;
    public string? BankBranch { get; set; }
    public int AccountId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class TreasuryBalanceSummaryDto
{
    public int? BranchId { get; set; }
    public decimal TotalCashBalance { get; set; }
    public decimal TotalBankBalance { get; set; }
    public decimal TotalBalance { get; set; }
    public List<CashFundDto> CashFunds { get; set; } = [];
    public List<BankAccountDto> BankAccounts { get; set; } = [];
}

public class TreasuryPeriodSummaryDto
{
    public int? BranchId { get; set; }
    public DateTime From { get; set; }
    public DateTime To { get; set; }
    public decimal TotalReceipts { get; set; }
    public decimal TotalPayments { get; set; }
    public decimal NetCashFlow { get; set; }
}

public enum InternalTransferDirection
{
    CashToBank = 1,
    BankToCash = 2,
    BankToBank = 3
}

public class InternalTransferRequest
{
    public int BranchId { get; set; }
    public InternalTransferDirection Direction { get; set; }
    public int? CashFundId { get; set; }
    public int? BankAccountId { get; set; }
    public int? DestinationBankAccountId { get; set; }
    public decimal Amount { get; set; }
    public DateTime? TransferDate { get; set; }
    public string? Notes { get; set; }
}

public class InternalTransferResultDto
{
    public int JournalEntryId { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? CashFundBalance { get; set; }
    public decimal BankAccountBalance { get; set; }
    public decimal? DestinationBankAccountBalance { get; set; }
}

public class JournalEntryLineDto
{
    public int AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public int? PartnerId { get; set; }
    public string? Description { get; set; }
}

public class JournalEntrySummaryDto
{
    public int Id { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public List<JournalEntryLineDto> Lines { get; set; } = [];
}

public class ReceiptAllocationInput
{
    public int SalesInvoiceId { get; set; }
    public decimal AllocatedAmount { get; set; }
}

public class ReceiptAllocationDto
{
    public int Id { get; set; }
    public int SalesInvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public decimal AllocatedAmount { get; set; }
}

public class CreateReceiptVoucherRequest
{
    public DateTime? VoucherDate { get; set; }
    public int BranchId { get; set; }
    public TreasuryPaymentMethod PaymentMethod { get; set; } = TreasuryPaymentMethod.Cash;
    public int? CashFundId { get; set; }
    public int? BankAccountId { get; set; }
    public int? PartnerId { get; set; }
    public string PayerName { get; set; } = string.Empty;
    public string? PayerPhone { get; set; }
    public decimal Amount { get; set; }
    public ReceiptType ReceiptType { get; set; } = ReceiptType.CustomerPayment;
    public string? Notes { get; set; }
    public bool AutoAllocateFifo { get; set; } = false;
    public List<ReceiptAllocationInput> Allocations { get; set; } = [];
}

public class ReceiptVoucherDto
{
    public int Id { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public DateTime VoucherDate { get; set; }
    public int BranchId { get; set; }
    public string? BranchName { get; set; }
    public TreasuryPaymentMethod PaymentMethod { get; set; }
    public int? CashFundId { get; set; }
    public int? BankAccountId { get; set; }
    public string? FundOrBankName { get; set; }
    public int? PartnerId { get; set; }
    public string? PartnerName { get; set; }
    public string PayerName { get; set; } = string.Empty;
    public string? PayerPhone { get; set; }
    public decimal Amount { get; set; }
    public ReceiptType ReceiptType { get; set; }
    public VoucherStatus Status { get; set; }
    public int? JournalEntryId { get; set; }
    public JournalEntrySummaryDto? JournalEntry { get; set; }
    public string? Notes { get; set; }
    public List<ReceiptAllocationDto> Allocations { get; set; } = [];
}

public class CreatePaymentVoucherRequest
{
    public DateTime? VoucherDate { get; set; }
    public int BranchId { get; set; }
    public TreasuryPaymentMethod PaymentMethod { get; set; } = TreasuryPaymentMethod.Cash;
    public int? CashFundId { get; set; }
    public int? BankAccountId { get; set; }
    public int? PartnerId { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public PaymentType PaymentType { get; set; } = PaymentType.SupplierPayment;
    public int? ExpenseAccountId { get; set; }
    public string? Notes { get; set; }
}

public class PaymentVoucherDto
{
    public int Id { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public DateTime VoucherDate { get; set; }
    public int BranchId { get; set; }
    public string? BranchName { get; set; }
    public TreasuryPaymentMethod PaymentMethod { get; set; }
    public int? CashFundId { get; set; }
    public int? BankAccountId { get; set; }
    public string? FundOrBankName { get; set; }
    public int? PartnerId { get; set; }
    public string? PartnerName { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public PaymentType PaymentType { get; set; }
    public int? ExpenseAccountId { get; set; }
    public VoucherStatus Status { get; set; }
    public int? JournalEntryId { get; set; }
    public JournalEntrySummaryDto? JournalEntry { get; set; }
    public string? Notes { get; set; }
}

public class ReverseVoucherRequest
{
    public string? Reason { get; set; }
}
