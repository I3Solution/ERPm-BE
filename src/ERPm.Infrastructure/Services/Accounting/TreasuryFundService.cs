using ERPm.Application.Common;
using ERPm.Application.DTOs.Accounting;
using ERPm.Application.Features.Accounting;
using ERPm.Application.Features.System;
using ERPm.Domain.Entities.Accounting;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;
using static ERPm.Infrastructure.Services.Common.PostingTransaction;

namespace ERPm.Infrastructure.Services.Accounting;

public class TreasuryFundService(
    ApplicationDbContext context,
    IBranchService branches,
    PostingTransaction posting,
    IDynamicPostingEngine engine) : ITreasuryFundService
{
    public async Task<List<CashFundDto>> GetCashFundsAsync(int? branchId = null, CancellationToken ct = default)
    {
        var query = context.CashFunds.AsNoTracking()
            .Include(f => f.Branch)
            .Include(f => f.Account)
            .AsQueryable();

        if (branchId is > 0)
        {
            var ids = await branches.GetBranchAndDescendantIdsAsync(branchId.Value, ct);
            query = query.Where(f => ids.Contains(f.BranchId));
        }

        var items = await query.OrderBy(f => f.Code).ToListAsync(ct);
        return items.Select(MapCashFund).ToList();
    }

    public async Task<CashFundDto?> GetCashFundAsync(int id, CancellationToken ct = default)
    {
        var item = await context.CashFunds.AsNoTracking()
            .Include(f => f.Branch)
            .Include(f => f.Account)
            .FirstOrDefaultAsync(f => f.Id == id, ct);
        return item == null ? null : MapCashFund(item);
    }

    public async Task<CashFundDto> CreateCashFundAsync(CreateCashFundRequest request, CancellationToken ct = default)
    {
        Require(!string.IsNullOrWhiteSpace(request.Code) && !string.IsNullOrWhiteSpace(request.Name), "error.validation.request");
        Require(request.BranchId > 0 && request.AccountId > 0, "error.validation.request");
        ValidateNumber(request.OpeningBalance);

        var code = request.Code.Trim();
        var duplicate = await context.CashFunds.AnyAsync(f => f.Code == code, ct);
        Require(!duplicate, "error.request.conflict", 409);

        await ValidatePostableAccountAsync(request.AccountId, ct);

        var entity = new CashFund
        {
            Code = code,
            Name = request.Name.Trim(),
            BranchId = request.BranchId,
            AccountId = request.AccountId,
            CustodianEmployeeId = request.CustodianEmployeeId,
            CurrentBalance = Round(request.OpeningBalance),
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "VND" : request.Currency.Trim().ToUpperInvariant(),
            IsActive = request.IsActive
        };

        context.CashFunds.Add(entity);
        await context.SaveChangesAsync(ct);
        return (await GetCashFundAsync(entity.Id, ct))!;
    }

    public async Task<CashFundDto> UpdateCashFundAsync(int id, UpdateCashFundRequest request, CancellationToken ct = default)
    {
        Require(!string.IsNullOrWhiteSpace(request.Name) && request.AccountId > 0, "error.validation.request");
        var entity = await context.CashFunds.FirstOrDefaultAsync(f => f.Id == id, ct)
            ?? throw new BusinessException("error.treasury.fund_not_found", 404);

        await ValidatePostableAccountAsync(request.AccountId, ct);

        entity.Name = request.Name.Trim();
        entity.AccountId = request.AccountId;
        entity.CustodianEmployeeId = request.CustodianEmployeeId;
        entity.IsActive = request.IsActive;

        await context.SaveChangesAsync(ct);
        return (await GetCashFundAsync(entity.Id, ct))!;
    }

    public async Task DeleteCashFundAsync(int id, CancellationToken ct = default)
    {
        var entity = await context.CashFunds.FirstOrDefaultAsync(f => f.Id == id, ct)
            ?? throw new BusinessException("error.treasury.fund_not_found", 404);
        entity.IsDeleted = true;
        await context.SaveChangesAsync(ct);
    }

    public async Task<List<BankAccountDto>> GetBankAccountsAsync(int? branchId = null, CancellationToken ct = default)
    {
        var query = context.BankAccounts.AsNoTracking()
            .Include(b => b.Branch)
            .Include(b => b.Account)
            .AsQueryable();

        if (branchId is > 0)
        {
            var ids = await branches.GetBranchAndDescendantIdsAsync(branchId.Value, ct);
            query = query.Where(b => ids.Contains(b.BranchId));
        }

        var items = await query.OrderBy(b => b.BankName).ThenBy(b => b.AccountNumber).ToListAsync(ct);
        return items.Select(MapBankAccount).ToList();
    }

    public async Task<BankAccountDto?> GetBankAccountAsync(int id, CancellationToken ct = default)
    {
        var item = await context.BankAccounts.AsNoTracking()
            .Include(b => b.Branch)
            .Include(b => b.Account)
            .FirstOrDefaultAsync(b => b.Id == id, ct);
        return item == null ? null : MapBankAccount(item);
    }

    public async Task<BankAccountDto> CreateBankAccountAsync(CreateBankAccountRequest request, CancellationToken ct = default)
    {
        Require(
            !string.IsNullOrWhiteSpace(request.BankName) &&
            !string.IsNullOrWhiteSpace(request.AccountNumber) &&
            !string.IsNullOrWhiteSpace(request.AccountHolder),
            "error.validation.request");
        Require(request.BranchId > 0 && request.AccountId > 0, "error.validation.request");
        ValidateNumber(request.OpeningBalance);

        var accountNumber = request.AccountNumber.Trim();
        var duplicate = await context.BankAccounts.IgnoreQueryFilters()
            .AnyAsync(b => !b.IsDeleted && b.AccountNumber == accountNumber, ct);
        Require(!duplicate, "error.request.conflict", 409);

        await ValidatePostableAccountAsync(request.AccountId, ct);

        var entity = new BankAccount
        {
            BankName = request.BankName.Trim(),
            AccountNumber = accountNumber,
            AccountHolder = request.AccountHolder.Trim(),
            BankBranch = request.BankBranch?.Trim(),
            BranchId = request.BranchId,
            AccountId = request.AccountId,
            CurrentBalance = Round(request.OpeningBalance),
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "VND" : request.Currency.Trim().ToUpperInvariant(),
            IsActive = request.IsActive
        };

        context.BankAccounts.Add(entity);
        await context.SaveChangesAsync(ct);
        return (await GetBankAccountAsync(entity.Id, ct))!;
    }

    public async Task<BankAccountDto> UpdateBankAccountAsync(int id, UpdateBankAccountRequest request, CancellationToken ct = default)
    {
        Require(
            !string.IsNullOrWhiteSpace(request.BankName) &&
            !string.IsNullOrWhiteSpace(request.AccountHolder) &&
            request.AccountId > 0,
            "error.validation.request");

        var entity = await context.BankAccounts.FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new BusinessException("error.treasury.bank_account_not_found", 404);

        await ValidatePostableAccountAsync(request.AccountId, ct);

        entity.BankName = request.BankName.Trim();
        entity.AccountHolder = request.AccountHolder.Trim();
        entity.BankBranch = request.BankBranch?.Trim();
        entity.AccountId = request.AccountId;
        entity.IsActive = request.IsActive;

        await context.SaveChangesAsync(ct);
        return (await GetBankAccountAsync(entity.Id, ct))!;
    }

    public async Task DeleteBankAccountAsync(int id, CancellationToken ct = default)
    {
        var entity = await context.BankAccounts.FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new BusinessException("error.treasury.bank_account_not_found", 404);
        entity.IsDeleted = true;
        await context.SaveChangesAsync(ct);
    }

    public async Task<TreasuryBalanceSummaryDto> GetBalancesAsync(int? branchId = null, CancellationToken ct = default)
    {
        var cashFunds = await GetCashFundsAsync(branchId, ct);
        var bankAccounts = await GetBankAccountsAsync(branchId, ct);
        var totalCash = Round(cashFunds.Where(f => f.IsActive).Sum(f => f.CurrentBalance));
        var totalBank = Round(bankAccounts.Where(b => b.IsActive).Sum(b => b.CurrentBalance));

        return new TreasuryBalanceSummaryDto
        {
            BranchId = branchId,
            TotalCashBalance = totalCash,
            TotalBankBalance = totalBank,
            TotalBalance = Round(totalCash + totalBank),
            CashFunds = cashFunds,
            BankAccounts = bankAccounts
        };
    }

    public Task<InternalTransferResultDto> TransferInternalAsync(InternalTransferRequest request, CancellationToken ct = default)
    {
        Require(request.BranchId > 0, "error.validation.request");
        Require(Enum.IsDefined(request.Direction), "error.validation.request");
        ValidateNumber(request.Amount, positive: true);

        if (request.Direction == InternalTransferDirection.BankToBank)
        {
            Require(request.BankAccountId is > 0 && request.DestinationBankAccountId is > 0, "error.validation.request");
            Require(request.BankAccountId != request.DestinationBankAccountId, "error.treasury.invalid_transfer", 400);
        }
        else
        {
            Require(request.CashFundId is > 0 && request.BankAccountId is > 0, "error.validation.request");
        }

        return posting.RunAsync(async () =>
        {
            var amount = Round(request.Amount);
            int debitAccountId;
            int creditAccountId;
            decimal? cashFundBalance = null;
            decimal bankAccountBalance;
            decimal? destinationBankBalance = null;

            if (request.Direction == InternalTransferDirection.BankToBank)
            {
                var srcId = request.BankAccountId!.Value;
                var dstId = request.DestinationBankAccountId!.Value;
                // Lock bank accounts in ascending Id order to avoid deadlocks.
                var firstId = Math.Min(srcId, dstId);
                var secondId = Math.Max(srcId, dstId);
                await posting.LockAsync($"treasury-bank:{firstId}", ct);
                await posting.LockAsync($"treasury-bank:{secondId}", ct);

                var sourceBank = await context.BankAccounts
                    .FirstOrDefaultAsync(b => b.Id == srcId && b.IsActive, ct)
                    ?? throw new BusinessException("error.treasury.bank_account_not_found", 404);
                var destBank = await context.BankAccounts
                    .FirstOrDefaultAsync(b => b.Id == dstId && b.IsActive, ct)
                    ?? throw new BusinessException("error.treasury.bank_account_not_found", 404);

                Require(sourceBank.BranchId == request.BranchId && destBank.BranchId == request.BranchId, "error.validation.request");

                TreasuryPostingHelper.DebitFundBalance(null, sourceBank, amount);
                TreasuryPostingHelper.CreditFundBalance(null, destBank, amount);
                debitAccountId = destBank.AccountId;
                creditAccountId = sourceBank.AccountId;
                bankAccountBalance = sourceBank.CurrentBalance;
                destinationBankBalance = destBank.CurrentBalance;
            }
            else
            {
                var cashFundId = request.CashFundId!.Value;
                var bankAccountId = request.BankAccountId!.Value;
                // Lock in deterministic order (cash then bank) to avoid deadlocks.
                await posting.LockAsync($"treasury-cash:{cashFundId}", ct);
                await posting.LockAsync($"treasury-bank:{bankAccountId}", ct);

                var cashFund = await context.CashFunds
                    .FirstOrDefaultAsync(f => f.Id == cashFundId && f.IsActive, ct)
                    ?? throw new BusinessException("error.treasury.fund_not_found", 404);
                var bankAccount = await context.BankAccounts
                    .FirstOrDefaultAsync(b => b.Id == bankAccountId && b.IsActive, ct)
                    ?? throw new BusinessException("error.treasury.bank_account_not_found", 404);

                Require(cashFund.BranchId == request.BranchId && bankAccount.BranchId == request.BranchId, "error.validation.request");

                if (request.Direction == InternalTransferDirection.CashToBank)
                {
                    TreasuryPostingHelper.DebitFundBalance(cashFund, null, amount);
                    TreasuryPostingHelper.CreditFundBalance(null, bankAccount, amount);
                    debitAccountId = bankAccount.AccountId;
                    creditAccountId = cashFund.AccountId;
                }
                else
                {
                    TreasuryPostingHelper.DebitFundBalance(null, bankAccount, amount);
                    TreasuryPostingHelper.CreditFundBalance(cashFund, null, amount);
                    debitAccountId = cashFund.AccountId;
                    creditAccountId = bankAccount.AccountId;
                }

                cashFundBalance = cashFund.CurrentBalance;
                bankAccountBalance = bankAccount.CurrentBalance;
            }

            var transferDate = request.TransferDate ?? DateTime.UtcNow;
            var voucherNumber = await posting.NumberAsync("CTNB", ct);
            var description = string.IsNullOrWhiteSpace(request.Notes)
                ? $"Internal transfer {voucherNumber}"
                : request.Notes.Trim();

            var journal = new JournalEntry
            {
                VoucherNumber = voucherNumber,
                VoucherType = "INTERNAL_TRANSFER",
                DocDate = transferDate,
                PostingDate = transferDate,
                BranchId = request.BranchId,
                Description = description,
                Status = VoucherStatus.Posted
            };
            journal.Lines.Add(new JournalEntryLine
            {
                AccountId = debitAccountId,
                DebitAmount = amount,
                CreditAmount = 0m,
                Description = description
            });
            journal.Lines.Add(new JournalEntryLine
            {
                AccountId = creditAccountId,
                DebitAmount = 0m,
                CreditAmount = amount,
                Description = description
            });

            await engine.ValidateJournalAsync(journal, ct);
            context.JournalEntries.Add(journal);
            await context.SaveChangesAsync(ct);

            return new InternalTransferResultDto
            {
                JournalEntryId = journal.Id,
                VoucherNumber = journal.VoucherNumber,
                Amount = amount,
                CashFundBalance = cashFundBalance,
                BankAccountBalance = bankAccountBalance,
                DestinationBankAccountBalance = destinationBankBalance
            };
        }, ct);
    }

    private async Task ValidatePostableAccountAsync(int accountId, CancellationToken ct)
    {
        var account = await context.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountId, ct)
            ?? throw new BusinessException("error.accounting.accounts_missing", 404);
        Require(account.IsActive && account.IsPostable, "error.accounting.account_not_postable", 400);
    }

    private static CashFundDto MapCashFund(CashFund f) => new()
    {
        Id = f.Id,
        Code = f.Code,
        Name = f.Name,
        BranchId = f.BranchId,
        BranchName = f.Branch?.Name,
        AccountId = f.AccountId,
        AccountCode = f.Account?.Code,
        CustodianEmployeeId = f.CustodianEmployeeId,
        CurrentBalance = f.CurrentBalance,
        Currency = f.Currency,
        IsActive = f.IsActive
    };

    private static BankAccountDto MapBankAccount(BankAccount b) => new()
    {
        Id = b.Id,
        BankName = b.BankName,
        AccountNumber = b.AccountNumber,
        AccountHolder = b.AccountHolder,
        BankBranch = b.BankBranch,
        BranchId = b.BranchId,
        BranchName = b.Branch?.Name,
        AccountId = b.AccountId,
        AccountCode = b.Account?.Code,
        CurrentBalance = b.CurrentBalance,
        Currency = b.Currency,
        IsActive = b.IsActive
    };
}
