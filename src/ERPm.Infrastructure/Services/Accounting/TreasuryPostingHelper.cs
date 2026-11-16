using ERPm.Application.Common;
using ERPm.Application.Features.Accounting;
using ERPm.Domain.Entities.Accounting;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;
using static ERPm.Infrastructure.Services.Common.PostingTransaction;

namespace ERPm.Infrastructure.Services.Accounting;

internal static class TreasuryPostingHelper
{
    internal static async Task<(CashFund? CashFund, BankAccount? BankAccount, int GlAccountId)> ResolveAndLockFundAsync(
        ApplicationDbContext context,
        PostingTransaction posting,
        TreasuryPaymentMethod method,
        int? cashFundId,
        int? bankAccountId,
        int branchId,
        CancellationToken ct)
    {
        if (method == TreasuryPaymentMethod.Cash)
        {
            Require(cashFundId is > 0 && bankAccountId is null or 0, "error.validation.request");
            await posting.LockAsync($"treasury-cash:{cashFundId!.Value}", ct);
            var fund = await context.CashFunds
                .FirstOrDefaultAsync(f => f.Id == cashFundId.Value && f.IsActive, ct)
                ?? throw new BusinessException("error.treasury.fund_not_found", 404);
            Require(fund.BranchId == branchId, "error.validation.request");
            return (fund, null, fund.AccountId);
        }

        if (method == TreasuryPaymentMethod.Bank)
        {
            Require(bankAccountId is > 0 && cashFundId is null or 0, "error.validation.request");
            await posting.LockAsync($"treasury-bank:{bankAccountId!.Value}", ct);
            var bank = await context.BankAccounts
                .FirstOrDefaultAsync(b => b.Id == bankAccountId.Value && b.IsActive, ct)
                ?? throw new BusinessException("error.treasury.bank_account_not_found", 404);
            Require(bank.BranchId == branchId, "error.validation.request");
            return (null, bank, bank.AccountId);
        }

        throw new BusinessException("error.validation.payment_method", 400);
    }

    internal static void CreditFundBalance(CashFund? cashFund, BankAccount? bankAccount, decimal amount)
    {
        if (cashFund != null)
        {
            cashFund.CurrentBalance = Round(cashFund.CurrentBalance + amount);
        }
        else if (bankAccount != null)
        {
            bankAccount.CurrentBalance = Round(bankAccount.CurrentBalance + amount);
        }
    }

    internal static void DebitFundBalance(CashFund? cashFund, BankAccount? bankAccount, decimal amount)
    {
        if (cashFund != null)
        {
            Require(cashFund.CurrentBalance >= amount, "error.treasury.insufficient_fund_balance", 409);
            cashFund.CurrentBalance = Round(cashFund.CurrentBalance - amount);
        }
        else if (bankAccount != null)
        {
            Require(bankAccount.CurrentBalance >= amount, "error.treasury.insufficient_fund_balance", 409);
            bankAccount.CurrentBalance = Round(bankAccount.CurrentBalance - amount);
        }
    }

    internal static async Task<JournalEntry> BuildReversalJournalAsync(
        ApplicationDbContext context,
        PostingTransaction posting,
        IDynamicPostingEngine engine,
        int originalJournalId,
        string description,
        CancellationToken ct)
    {
        var original = await context.JournalEntries
            .Include(j => j.Lines)
            .FirstOrDefaultAsync(j => j.Id == originalJournalId, ct)
            ?? throw new BusinessException("error.accounting.accounts_missing", 404);

        var reversal = new JournalEntry
        {
            ReversesJournalEntryId = original.Id,
            VoucherNumber = await posting.NumberAsync("PKT", ct),
            VoucherType = original.VoucherType,
            DocDate = DateTime.UtcNow,
            PostingDate = DateTime.UtcNow,
            BranchId = original.BranchId,
            Description = description,
            Status = VoucherStatus.Posted,
            ReferenceId = original.ReferenceId
        };

        foreach (var line in original.Lines)
        {
            reversal.Lines.Add(new JournalEntryLine
            {
                AccountId = line.AccountId,
                DebitAmount = line.CreditAmount,
                CreditAmount = line.DebitAmount,
                PartnerId = line.PartnerId,
                Description = description
            });
        }

        await engine.ValidateJournalAsync(reversal, ct);
        context.JournalEntries.Add(reversal);
        return reversal;
    }
}
