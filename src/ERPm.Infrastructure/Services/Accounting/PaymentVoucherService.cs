using ERPm.Application.Common;
using ERPm.Application.DTOs.Accounting;
using ERPm.Application.Features.Accounting;
using ERPm.Application.Features.System;
using ERPm.Domain.Entities.Accounting;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;
using PartnerEntity = ERPm.Domain.Entities.Partner.Partner;
using static ERPm.Infrastructure.Services.Common.PostingTransaction;

namespace ERPm.Infrastructure.Services.Accounting;

public class PaymentVoucherService(
    ApplicationDbContext context,
    IBranchService branches,
    PostingTransaction posting,
    IDynamicPostingEngine engine) : IPaymentVoucherService
{
    public async Task<PagedResult<PaymentVoucherDto>> GetPaymentVouchersAsync(
        int? branchId = null,
        int? partnerId = null,
        string? q = null,
        DateTime? from = null,
        DateTime? to = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = context.PaymentVouchers.AsNoTracking()
            .Include(v => v.Branch)
            .Include(v => v.CashFund)
            .Include(v => v.BankAccount)
            .Include(v => v.Partner)
            .AsQueryable();

        if (branchId is > 0)
        {
            var ids = await branches.GetBranchAndDescendantIdsAsync(branchId.Value, ct);
            query = query.Where(v => ids.Contains(v.BranchId));
        }

        if (partnerId is > 0)
        {
            query = query.Where(v => v.PartnerId == partnerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(v =>
                v.VoucherNumber.ToLower().Contains(term) ||
                v.RecipientName.ToLower().Contains(term) ||
                (v.Notes != null && v.Notes.ToLower().Contains(term)));
        }

        if (from.HasValue)
        {
            query = query.Where(v => v.VoucherDate >= from.Value.AsUtc());
        }

        if (to.HasValue)
        {
            query = query.Where(v => v.VoucherDate <= to.Value.AsUtc());
        }

        var totalCount = await query.CountAsync(ct);
        var list = await query
            .OrderByDescending(v => v.VoucherDate)
            .ThenByDescending(v => v.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<PaymentVoucherDto>
        {
            Items = list.Select(Map).ToList(),
            TotalCount = totalCount,
            PageIndex = page,
            PageSize = pageSize
        };
    }

    public async Task<PaymentVoucherDto?> GetPaymentVoucherAsync(int id, CancellationToken ct = default)
    {
        var voucher = await context.PaymentVouchers.AsNoTracking()
            .Include(v => v.Branch)
            .Include(v => v.CashFund)
            .Include(v => v.BankAccount)
            .Include(v => v.Partner)
            .Include(v => v.JournalEntry)
                .ThenInclude(j => j!.Lines)
                    .ThenInclude(l => l.Account)
            .FirstOrDefaultAsync(v => v.Id == id, ct);
        return voucher == null ? null : Map(voucher);
    }

    public Task<PaymentVoucherDto> CreatePaymentVoucherAsync(CreatePaymentVoucherRequest request, CancellationToken ct = default)
    {
        Require(request.BranchId > 0 && !string.IsNullOrWhiteSpace(request.RecipientName), "error.validation.request");
        Require(Enum.IsDefined(request.PaymentMethod), "error.validation.payment_method");
        Require(Enum.IsDefined(request.PaymentType), "error.validation.request");
        ValidateNumber(request.Amount, positive: true);

        return posting.RunAsync(async () =>
        {
            var (cashFund, bankAccount, fundGlAccountId) = await TreasuryPostingHelper.ResolveAndLockFundAsync(
                context, posting, request.PaymentMethod, request.CashFundId, request.BankAccountId, request.BranchId, ct);

            var amount = Round(request.Amount);
            TreasuryPostingHelper.DebitFundBalance(cashFund, bankAccount, amount);

            PartnerEntity? partner = null;
            if (request.PartnerId is > 0)
            {
                await posting.LockAsync($"partner:{request.PartnerId.Value}", ct);
                partner = await context.Partners.FirstOrDefaultAsync(p => p.Id == request.PartnerId.Value, ct)
                    ?? throw new BusinessException("error.validation.request", 404);

                if (request.PaymentType == PaymentType.SupplierPayment)
                {
                    // DebtBalance is positive for receivable and negative for payable; paying a supplier reduces payable (increases DebtBalance).
                    partner.DebtBalance = Round(partner.DebtBalance + amount);
                }
            }

            var debitAccountId = await ResolveDebitAccountIdAsync(request, ct);
            var voucherDate = (request.VoucherDate ?? DateTime.UtcNow).AsUtc();
            var voucherNumber = await posting.NumberAsync("PC", ct);
            var description = string.IsNullOrWhiteSpace(request.Notes)
                ? $"Payment {voucherNumber} - {request.RecipientName.Trim()}"
                : request.Notes.Trim();

            var journal = new JournalEntry
            {
                VoucherNumber = voucherNumber,
                VoucherType = "PAYMENT",
                DocDate = voucherDate,
                PostingDate = voucherDate,
                BranchId = request.BranchId,
                Description = description,
                Status = VoucherStatus.Posted
            };
            journal.Lines.Add(new JournalEntryLine
            {
                AccountId = debitAccountId,
                DebitAmount = amount,
                CreditAmount = 0m,
                PartnerId = partner?.Id,
                Description = description
            });
            journal.Lines.Add(new JournalEntryLine
            {
                AccountId = fundGlAccountId,
                DebitAmount = 0m,
                CreditAmount = amount,
                PartnerId = partner?.Id,
                Description = description
            });

            await engine.ValidateJournalAsync(journal, ct);
            context.JournalEntries.Add(journal);

            var voucher = new PaymentVoucher
            {
                VoucherNumber = voucherNumber,
                VoucherDate = voucherDate,
                BranchId = request.BranchId,
                PaymentMethod = request.PaymentMethod,
                CashFundId = cashFund?.Id,
                BankAccountId = bankAccount?.Id,
                PartnerId = partner?.Id,
                RecipientName = request.RecipientName.Trim(),
                Amount = amount,
                PaymentType = request.PaymentType,
                ExpenseAccountId = debitAccountId,
                Status = VoucherStatus.Posted,
                JournalEntry = journal,
                Notes = request.Notes?.Trim()
            };
            context.PaymentVouchers.Add(voucher);
            await context.SaveChangesAsync(ct);

            if (journal.ReferenceId == null)
            {
                await context.JournalEntries
                    .Where(j => j.Id == journal.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(j => j.ReferenceId, voucher.Id), ct);
            }

            return (await GetPaymentVoucherAsync(voucher.Id, ct))!;
        }, ct);
    }

    public Task<PaymentVoucherDto> ReversePaymentVoucherAsync(int id, ReverseVoucherRequest? request = null, CancellationToken ct = default)
    {
        Require(id > 0, "error.validation.request");

        return posting.RunAsync(async () =>
        {
            await posting.LockAsync($"payment-voucher:{id}", ct);
            var voucher = await context.PaymentVouchers
                .FirstOrDefaultAsync(v => v.Id == id, ct)
                ?? throw new BusinessException("error.validation.request", 404);

            Require(voucher.Status != VoucherStatus.Cancelled, "error.treasury.voucher_already_cancelled", 409);
            Require(voucher.Status == VoucherStatus.Posted, "error.treasury.voucher_not_posted", 409);

            var (cashFund, bankAccount, _) = await TreasuryPostingHelper.ResolveAndLockFundAsync(
                context, posting, voucher.PaymentMethod, voucher.CashFundId, voucher.BankAccountId, voucher.BranchId, ct);

            TreasuryPostingHelper.CreditFundBalance(cashFund, bankAccount, voucher.Amount);

            if (voucher.PartnerId is > 0 && voucher.PaymentType == PaymentType.SupplierPayment)
            {
                await posting.LockAsync($"partner:{voucher.PartnerId.Value}", ct);
                var partner = await context.Partners.FirstOrDefaultAsync(p => p.Id == voucher.PartnerId.Value, ct);
                if (partner != null)
                {
                    partner.DebtBalance = Round(partner.DebtBalance - voucher.Amount);
                }
            }

            if (voucher.JournalEntryId is > 0)
            {
                var reason = string.IsNullOrWhiteSpace(request?.Reason)
                    ? $"Reverse payment {voucher.VoucherNumber}"
                    : $"Reverse payment {voucher.VoucherNumber}: {request.Reason.Trim()}";
                await TreasuryPostingHelper.BuildReversalJournalAsync(
                    context, posting, engine, voucher.JournalEntryId.Value, reason, ct);
            }

            voucher.Status = VoucherStatus.Cancelled;
            await context.SaveChangesAsync(ct);

            return (await GetPaymentVoucherAsync(voucher.Id, ct))!;
        }, ct);
    }

    private async Task<int> ResolveDebitAccountIdAsync(CreatePaymentVoucherRequest request, CancellationToken ct)
    {
        if (request.ExpenseAccountId is > 0)
        {
            var account = await context.Accounts.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == request.ExpenseAccountId.Value, ct)
                ?? throw new BusinessException("error.accounting.accounts_missing", 404);
            Require(account.IsActive && account.IsPostable, "error.accounting.account_not_postable", 400);
            return account.Id;
        }

        var dim = request.PaymentMethod == TreasuryPaymentMethod.Bank ? "BANK" : "CASH";
        return request.PaymentType switch
        {
            PaymentType.SupplierPayment =>
                (await engine.ResolvePostingRuleAsync("ACC", "PAYMENT_SUPPLIER", request.BranchId, dim, ct)).DebitAccountId,
            PaymentType.ExpensePayment =>
                (await engine.ResolvePostingRuleAsync("ACC", "PAYMENT_EXPENSE", request.BranchId, dim, ct)).DebitAccountId,
            PaymentType.SalaryPayment =>
                (await engine.ResolvePostingRuleAsync("ACC", "PAYMENT_SALARY", request.BranchId, dim, ct)).DebitAccountId,
            PaymentType.CashDepositToBank =>
                (await engine.ResolvePostingRuleAsync("ACC", "INTERNAL_TRANSFER", request.BranchId, "CASH_TO_BANK", ct)).DebitAccountId,
            PaymentType.EmployeeAdvance =>
                (await engine.ResolvePostingRuleAsync("ACC", "PAYMENT_ADVANCE", request.BranchId, dim, ct)).DebitAccountId,
            PaymentType.TaxPayment =>
                (await engine.ResolvePostingRuleAsync("ACC", "PAYMENT_TAX", request.BranchId, dim, ct)).DebitAccountId,
            PaymentType.OtherPayment =>
                (await engine.ResolvePostingRuleAsync("ACC", "PAYMENT_OTHER", request.BranchId, dim, ct)).DebitAccountId,
            _ => throw new BusinessException("error.validation.request", 400)
        };
    }

    private static PaymentVoucherDto Map(PaymentVoucher v) => new()
    {
        Id = v.Id,
        VoucherNumber = v.VoucherNumber,
        VoucherDate = v.VoucherDate,
        BranchId = v.BranchId,
        BranchName = v.Branch?.Name,
        PaymentMethod = v.PaymentMethod,
        CashFundId = v.CashFundId,
        BankAccountId = v.BankAccountId,
        FundOrBankName = v.CashFund != null
            ? v.CashFund.Name
            : v.BankAccount != null
                ? $"{v.BankAccount.BankName} - {v.BankAccount.AccountNumber}"
                : null,
        PartnerId = v.PartnerId,
        PartnerName = v.Partner?.Name,
        RecipientName = v.RecipientName,
        Amount = v.Amount,
        PaymentType = v.PaymentType,
        ExpenseAccountId = v.ExpenseAccountId,
        Status = v.Status,
        JournalEntryId = v.JournalEntryId,
        JournalEntry = v.JournalEntry == null ? null : new JournalEntrySummaryDto
        {
            Id = v.JournalEntry.Id,
            VoucherNumber = v.JournalEntry.VoucherNumber,
            TotalDebit = v.JournalEntry.Lines.Sum(l => l.DebitAmount),
            TotalCredit = v.JournalEntry.Lines.Sum(l => l.CreditAmount),
            Lines = v.JournalEntry.Lines.Select(l => new JournalEntryLineDto
            {
                AccountId = l.AccountId,
                AccountCode = l.Account?.Code ?? string.Empty,
                AccountName = l.Account?.Name ?? string.Empty,
                DebitAmount = l.DebitAmount,
                CreditAmount = l.CreditAmount,
                PartnerId = l.PartnerId,
                Description = l.Description
            }).ToList()
        },
        Notes = v.Notes
    };
}
