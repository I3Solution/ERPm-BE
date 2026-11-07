namespace ERPm.Domain.Services.Sales;

public enum CreditLevel
{
    Passed,
    Warning,
    Exceeded
}

/// <summary>
/// The single place that decides whether a customer's credit exposure is acceptable, so order confirmation,
/// direct invoices and POS cannot drift apart. A customer flagged as unlimited is never warned or blocked;
/// the limit value itself is kept so switching the flag off restores the old behaviour.
/// </summary>
public static class CreditLimitPolicy
{
    public const decimal WarningRatio = 0.8m;

    public static CreditLevel Evaluate(bool isUnlimitedCredit, decimal creditLimit, decimal exposure)
    {
        if (isUnlimitedCredit) return CreditLevel.Passed;
        if (exposure > creditLimit) return CreditLevel.Exceeded;
        return exposure >= creditLimit * WarningRatio ? CreditLevel.Warning : CreditLevel.Passed;
    }
}
