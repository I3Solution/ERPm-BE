using ERPm.Domain.Entities.Accounting;

namespace ERPm.Application.DTOs.Accounting;

public sealed class ArAgingInvoiceDto
{
    public int InvoiceId { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime DueDate { get; set; }
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingDebt { get; set; }
    public int DaysOverdue { get; set; }
    public string AgingBucket { get; set; } = string.Empty;
    public bool IsBadDebtWarning { get; set; }
}

public sealed class ArAgingCustomerSummaryDto
{
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal Current { get; set; }
    public decimal Days1To30 { get; set; }
    public decimal Days31To60 { get; set; }
    public decimal Days61To90 { get; set; }
    public decimal Over90Days { get; set; }
    public decimal TotalRemainingDebt { get; set; }
    public int BadDebtInvoiceCount { get; set; }
}

public sealed class ArAgingReportDto
{
    public DateTime AsOfDate { get; set; }
    public decimal TotalCurrent { get; set; }
    public decimal Total1To30 { get; set; }
    public decimal Total31To60 { get; set; }
    public decimal Total61To90 { get; set; }
    public decimal TotalOver90 { get; set; }
    public decimal GrandTotal { get; set; }
    public List<ArAgingCustomerSummaryDto> Customers { get; set; } = [];
    public List<ArAgingInvoiceDto> Invoices { get; set; } = [];
}

public sealed class StatementTransactionDto
{
    public DateTime TransactionDate { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public decimal RunningBalance { get; set; }
}

public sealed class StatementOfAccountDto
{
    public int PartnerId { get; set; }
    public string PartnerCode { get; set; } = string.Empty;
    public string PartnerName { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal ClosingBalance { get; set; }
    public List<StatementTransactionDto> Transactions { get; set; } = [];
}

public sealed class GeneralJournalRowDto
{
    public int JournalEntryId { get; set; }
    public DateTime PostingDate { get; set; }
    public DateTime DocDate { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public string VoucherType { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string? PartnerName { get; set; }
    public string? Description { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
}

public sealed class GeneralJournalReportDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public bool IsBalanced => TotalDebit == TotalCredit;
    public List<GeneralJournalRowDto> Rows { get; set; } = [];
}

public sealed class GeneralLedgerRowDto
{
    public DateTime PostingDate { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public string VoucherType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal DebitAmount { get; set; }
    public decimal CreditAmount { get; set; }
    public decimal RunningBalance { get; set; }
}

public sealed class GeneralLedgerReportDto
{
    public int AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public decimal ClosingBalance { get; set; }
    public List<GeneralLedgerRowDto> Rows { get; set; } = [];
}

public sealed class TreasuryLedgerRowDto
{
    public DateTime VoucherDate { get; set; }
    public string VoucherNumber { get; set; } = string.Empty;
    public string Direction { get; set; } = string.Empty;
    public string CounterpartyName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal CashIn { get; set; }
    public decimal CashOut { get; set; }
    public decimal RunningBalance { get; set; }
}

public sealed class TreasuryLedgerReportDto
{
    public TreasuryPaymentMethod PaymentMethod { get; set; }
    public int FundOrBankId { get; set; }
    public string FundOrBankName { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal TotalIn { get; set; }
    public decimal TotalOut { get; set; }
    public decimal ClosingBalance { get; set; }
    public List<TreasuryLedgerRowDto> Rows { get; set; } = [];
}
