namespace ERPm.Domain.Entities.Accounting;

public enum AccountType
{
    Asset = 1,
    Liability = 2,
    Equity = 3,
    Revenue = 4,     // Revenue (5xx, 7xx)
    Expense = 5
}

public enum AccountNature
{
    DebitOnly = 1,
    CreditOnly = 2,
    Bilateral = 3
}

public enum VoucherStatus
{
    Draft = 0,
    Posted = 1,
    Cancelled = 2
}

public enum TreasuryPaymentMethod
{
    Cash = 1,
    Bank = 2
}

public enum ReceiptType
{
    CustomerPayment = 1,
    AdvanceFromCustomer = 2,
    BankWithdrawalToCash = 3,
    EmployeeRefund = 4,
    OtherReceipt = 99
}

public enum PaymentType
{
    SupplierPayment = 1,
    ExpensePayment = 2,
    SalaryPayment = 3,
    CashDepositToBank = 4,
    EmployeeAdvance = 5,
    TaxPayment = 6,
    OtherPayment = 99
}
