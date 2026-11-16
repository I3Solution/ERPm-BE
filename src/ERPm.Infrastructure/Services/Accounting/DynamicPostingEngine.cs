using ERPm.Application.Common;
using ERPm.Application.DTOs.Accounting;
using ERPm.Application.Features.Accounting;
using ERPm.Domain.Entities.Accounting;
using ERPm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERPm.Infrastructure.Services.Accounting;

public sealed class DynamicPostingEngine(ApplicationDbContext context) : IDynamicPostingEngine
{
    public async Task<PostingRuleResult> ResolvePostingRuleAsync(
        string module,
        string transactionType,
        int? branchId,
        string? dimensionKey,
        CancellationToken ct = default)
    {
        var mod = module.Trim().ToUpperInvariant();
        var txType = transactionType.Trim().ToUpperInvariant();
        var dim = string.IsNullOrWhiteSpace(dimensionKey) ? null : dimensionKey.Trim().ToUpperInvariant();

        var profile = await context.AccountPostingProfiles
            .AsNoTracking()
            .Include(p => p.DebitAccount)
            .Include(p => p.CreditAccount)
            .Where(p => p.IsActive
                && p.Module == mod
                && p.TransactionType == txType
                && p.DimensionKey == dim
                && (p.BranchId == branchId || p.BranchId == null))
            .OrderByDescending(p => p.BranchId.HasValue)
            .FirstOrDefaultAsync(ct);

        if (profile == null)
            throw new BusinessException("error.accounting.posting_profile_not_found", 404);

        if (profile.DebitAccount is not { IsActive: true } || profile.CreditAccount is not { IsActive: true })
            throw new BusinessException("error.accounting.accounts_missing", 409);

        if (!profile.DebitAccount.IsPostable || !profile.CreditAccount.IsPostable)
            throw new BusinessException("error.accounting.account_not_postable", 400);

        return new PostingRuleResult(
            profile.Id,
            profile.Module,
            profile.TransactionType,
            profile.BranchId,
            profile.DimensionKey,
            profile.DebitAccountId,
            profile.DebitAccount.Code,
            profile.CreditAccountId,
            profile.CreditAccount.Code);
    }

    public async Task<int> ResolveCashAccountIdAsync(int branchId, int? cashFundId, CancellationToken ct = default)
    {
        if (cashFundId.HasValue)
        {
            var fund = await context.CashFunds
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(f => !f.IsDeleted && f.Id == cashFundId.Value && f.BranchId == branchId && f.IsActive, ct);

            if (fund == null)
                throw new BusinessException("error.validation.reference", 404);

            return fund.AccountId;
        }

        var defaultFund = await context.CashFunds
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(f => !f.IsDeleted && f.BranchId == branchId && f.IsActive)
            .OrderBy(f => f.Id)
            .FirstOrDefaultAsync(ct);

        if (defaultFund != null)
            return defaultFund.AccountId;

        var rule = await ResolvePostingRuleAsync("SD", "SALES_REVENUE", branchId, "CASH", ct);
        return rule.DebitAccountId;
    }

    public async Task<int> ResolveBankAccountIdAsync(int branchId, int? bankAccountId, CancellationToken ct = default)
    {
        if (bankAccountId.HasValue)
        {
            var bank = await context.BankAccounts
                .AsNoTracking()
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(b => !b.IsDeleted && b.Id == bankAccountId.Value && b.BranchId == branchId && b.IsActive, ct);

            if (bank == null)
                throw new BusinessException("error.validation.reference", 404);

            return bank.AccountId;
        }

        var defaultBank = await context.BankAccounts
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(b => !b.IsDeleted && b.BranchId == branchId && b.IsActive)
            .OrderBy(b => b.Id)
            .FirstOrDefaultAsync(ct);

        if (defaultBank != null)
            return defaultBank.AccountId;

        var rule = await ResolvePostingRuleAsync("SD", "SALES_REVENUE", branchId, "BANK", ct);
        return rule.DebitAccountId;
    }

    public async Task<int> ResolveReceivableAccountIdAsync(int branchId, int? partnerId, CancellationToken ct = default)
    {
        if (partnerId.HasValue)
        {
            var receivableAccountId = await context.Partners
                .AsNoTracking()
                .Where(p => p.Id == partnerId.Value && p.IsActive)
                .Select(p => p.ReceivableAccountId)
                .FirstOrDefaultAsync(ct);

            if (receivableAccountId.HasValue)
                return receivableAccountId.Value;
        }

        var rule = await ResolvePostingRuleAsync("SD", "SALES_REVENUE", branchId, "DEBT", ct);
        return rule.DebitAccountId;
    }

    public async Task ValidateJournalAsync(JournalEntry journal, CancellationToken ct = default)
    {
        if (journal.Lines.Count < 2 ||
            journal.Lines.Any(l => l.AccountId <= 0 || l.DebitAmount < 0 || l.CreditAmount < 0 || (l.DebitAmount == 0) == (l.CreditAmount == 0)))
        {
            throw new BusinessException("error.accounting.unbalanced_journal", 400);
        }

        var totalDebit = journal.Lines.Sum(l => l.DebitAmount);
        var totalCredit = journal.Lines.Sum(l => l.CreditAmount);
        if (totalDebit <= 0 || totalDebit != totalCredit)
            throw new BusinessException("error.accounting.unbalanced_journal", 400);

        var accountIds = journal.Lines.Select(l => l.AccountId).Distinct().ToArray();
        var accounts = await context.Accounts
            .AsNoTracking()
            .Where(a => accountIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, ct);

        foreach (var line in journal.Lines)
        {
            if (!accounts.TryGetValue(line.AccountId, out var account) || !account.IsActive)
                throw new BusinessException("error.accounting.accounts_missing", 409);

            if (!account.IsPostable)
                throw new BusinessException("error.accounting.account_not_postable", 400);

            if (account.TrackPartner && !line.PartnerId.HasValue)
                throw new BusinessException("error.accounting.partner_required", 400);
        }
    }
}
