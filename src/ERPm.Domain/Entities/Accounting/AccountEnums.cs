namespace ERPm.Domain.Entities.Accounting;

public enum AccountType
{
    Asset = 1,
    Liability = 2,
    Equity = 3,
    Revenue = 4,     // Doanh thu (5xx, 7xx)
    Expense = 5
}

public enum VoucherStatus
{
    Draft = 0,
    Posted = 1,
    Cancelled = 2
}
