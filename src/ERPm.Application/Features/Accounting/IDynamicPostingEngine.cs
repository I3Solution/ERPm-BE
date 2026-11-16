using ERPm.Application.DTOs.Accounting;
using ERPm.Domain.Entities.Accounting;

namespace ERPm.Application.Features.Accounting;

public interface IDynamicPostingEngine
{
    /// <summary>
    /// Resolves the active posting rule with branch-level override priority, falling back to system-wide default (BranchId = null).
    /// </summary>
    Task<PostingRuleResult> ResolvePostingRuleAsync(
        string module,
        string transactionType,
        int? branchId,
        string? dimensionKey,
        CancellationToken ct = default);

    /// <summary>
    /// Resolves the cash GL account from CashFund.AccountId, falling back to the posting rule if no cash fund is specified/found.
    /// </summary>
    Task<int> ResolveCashAccountIdAsync(int branchId, int? cashFundId, CancellationToken ct = default);

    /// <summary>
    /// Resolves the bank GL account from BankAccount.AccountId, falling back to the posting rule if no bank account is specified/found.
    /// </summary>
    Task<int> ResolveBankAccountIdAsync(int branchId, int? bankAccountId, CancellationToken ct = default);

    /// <summary>
    /// Resolves the receivable GL account from Partner.ReceivableAccountId, falling back to the SALES_REVENUE/DEBT posting rule.
    /// </summary>
    Task<int> ResolveReceivableAccountIdAsync(int branchId, int? partnerId, CancellationToken ct = default);

    /// <summary>
    /// Validates double-entry balance (TotalDebit == TotalCredit), postability (IsPostable), and partner tracking (TrackPartner).
    /// </summary>
    Task ValidateJournalAsync(JournalEntry journal, CancellationToken ct = default);
}
