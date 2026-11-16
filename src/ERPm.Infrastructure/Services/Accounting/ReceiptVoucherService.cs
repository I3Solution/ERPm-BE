using ERPm.Application.Common;
using ERPm.Application.DTOs.Accounting;
using ERPm.Application.Features.Accounting;
using ERPm.Application.Features.System;
using ERPm.Domain.Entities.Accounting;
using ERPm.Domain.Entities.Sales;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;
using PartnerEntity = ERPm.Domain.Entities.Partner.Partner;
using static ERPm.Infrastructure.Services.Common.PostingTransaction;

namespace ERPm.Infrastructure.Services.Accounting;

public class ReceiptVoucherService(
    ApplicationDbContext context,
    IBranchService branches,
    PostingTransaction posting,
    IDynamicPostingEngine engine) : IReceiptVoucherService
{
    public async Task<List<ReceiptVoucherDto>> GetReceiptVouchersAsync(
        int? branchId = null,
        int? partnerId = null,
        CancellationToken ct = default)
    {
        var query = context.ReceiptVouchers.AsNoTracking()
            .Include(v => v.Allocations)
            .ThenInclude(a => a.SalesInvoice)
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

        var list = await query
            .OrderByDescending(v => v.VoucherDate)
            .ThenByDescending(v => v.Id)
            .Take(100)
            .ToListAsync(ct);

        return list.Select(Map).ToList();
    }

    public async Task<ReceiptVoucherDto?> GetReceiptVoucherAsync(int id, CancellationToken ct = default)
    {
        var voucher = await context.ReceiptVouchers.AsNoTracking()
            .Include(v => v.Allocations)
            .ThenInclude(a => a.SalesInvoice)
            .FirstOrDefaultAsync(v => v.Id == id, ct);

        return voucher == null ? null : Map(voucher);
    }

    public Task<ReceiptVoucherDto> CreateReceiptVoucherAsync(CreateReceiptVoucherRequest request, CancellationToken ct = default)
    {
        Require(request.BranchId > 0 && !string.IsNullOrWhiteSpace(request.PayerName), "error.validation.request");
        Require(Enum.IsDefined(request.PaymentMethod), "error.validation.payment_method");
        Require(Enum.IsDefined(request.ReceiptType), "error.validation.request");
        ValidateNumber(request.Amount, positive: true);

        var allocations = request.Allocations ?? [];
        if (allocations.Count > 0)
        {
            var distinctCount = allocations.Select(a => a.SalesInvoiceId).Distinct().Count();
            Require(distinctCount == allocations.Count, "error.validation.request");

            foreach (var alloc in allocations)
            {
                Require(alloc.SalesInvoiceId > 0, "error.validation.request");
                ValidateNumber(alloc.AllocatedAmount, positive: true);
            }

            var totalAllocated = Round(allocations.Sum(a => a.AllocatedAmount));
            Require(totalAllocated <= Round(request.Amount), "error.treasury.allocation_exceeds_amount", 400);
        }

        return posting.RunAsync(async () =>
        {
            var (cashFund, bankAccount, fundGlAccountId) = await TreasuryPostingHelper.ResolveAndLockFundAsync(
                context, posting, request.PaymentMethod, request.CashFundId, request.BankAccountId, request.BranchId, ct);

            PartnerEntity? partner = null;
            if (request.PartnerId is > 0)
            {
                await posting.LockAsync($"partner:{request.PartnerId.Value}", ct);
                partner = await context.Partners.FirstOrDefaultAsync(p => p.Id == request.PartnerId.Value, ct)
                    ?? throw new BusinessException("error.validation.request", 404);
            }

            var amount = Round(request.Amount);
            var voucherDate = request.VoucherDate ?? DateTime.UtcNow;
            var voucherNumber = await posting.NumberAsync("PT", ct);

            var voucher = new ReceiptVoucher
            {
                VoucherNumber = voucherNumber,
                VoucherDate = voucherDate,
                BranchId = request.BranchId,
                PaymentMethod = request.PaymentMethod,
                CashFundId = cashFund?.Id,
                BankAccountId = bankAccount?.Id,
                PartnerId = partner?.Id,
                PayerName = request.PayerName.Trim(),
                PayerPhone = request.PayerPhone?.Trim(),
                Amount = amount,
                ReceiptType = request.ReceiptType,
                Status = VoucherStatus.Posted,
                Notes = request.Notes?.Trim()
            };

            // Apply invoice allocations in deterministic order.
            foreach (var allocInput in allocations.OrderBy(a => a.SalesInvoiceId))
            {
                await posting.LockAsync($"sales-invoice:{allocInput.SalesInvoiceId}", ct);
                var invoice = await context.SalesInvoices
                    .FirstOrDefaultAsync(i => i.Id == allocInput.SalesInvoiceId, ct)
                    ?? throw new BusinessException("error.validation.request", 404);

                Require(invoice.Status == VoucherStatus.Posted, "error.treasury.voucher_not_posted", 409);
                if (partner != null)
                {
                    Require(invoice.CustomerId == partner.Id, "error.validation.request");
                }

                var allocAmount = Round(allocInput.AllocatedAmount);
                Require(allocAmount <= invoice.RemainingDebt, "error.treasury.allocation_exceeds_debt", 409);

                invoice.RemainingDebt = Round(invoice.RemainingDebt - allocAmount);
                invoice.PaidAmount = Round(invoice.PaidAmount + allocAmount);

                voucher.Allocations.Add(new ReceiptVoucherAllocation
                {
                    SalesInvoiceId = invoice.Id,
                    AllocatedAmount = allocAmount
                });
            }

            if (partner != null && request.ReceiptType is ReceiptType.CustomerPayment or ReceiptType.AdvanceFromCustomer)
            {
                partner.DebtBalance = Round(partner.DebtBalance - amount);
            }

            TreasuryPostingHelper.CreditFundBalance(cashFund, bankAccount, amount);

            var creditAccountId = await ResolveCreditAccountIdAsync(request, partner, ct);
            var description = string.IsNullOrWhiteSpace(request.Notes)
                ? $"Receipt {voucherNumber} - {voucher.PayerName}"
                : request.Notes.Trim();

            var journal = new JournalEntry
            {
                VoucherNumber = voucherNumber,
                VoucherType = "RECEIPT",
                DocDate = voucherDate,
                PostingDate = voucherDate,
                BranchId = request.BranchId,
                Description = description,
                Status = VoucherStatus.Posted
            };
            journal.Lines.Add(new JournalEntryLine
            {
                AccountId = fundGlAccountId,
                DebitAmount = amount,
                CreditAmount = 0m,
                PartnerId = partner?.Id,
                Description = description
            });
            journal.Lines.Add(new JournalEntryLine
            {
                AccountId = creditAccountId,
                DebitAmount = 0m,
                CreditAmount = amount,
                PartnerId = partner?.Id,
                Description = description
            });

            await engine.ValidateJournalAsync(journal, ct);
            context.JournalEntries.Add(journal);
            voucher.JournalEntry = journal;
            context.ReceiptVouchers.Add(voucher);

            if (voucher.Allocations.Count > 0)
            {
                // Updating RemainingDebt/PaidAmount on posted SalesInvoices requires AllowCancellation.
                context.AllowCancellation = true;
                try { await context.SaveChangesAsync(ct); }
                finally { context.AllowCancellation = false; }
            }
            else
            {
                await context.SaveChangesAsync(ct);
            }

            if (journal.ReferenceId == null)
            {
                await context.JournalEntries
                    .Where(j => j.Id == journal.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(j => j.ReferenceId, voucher.Id), ct);
            }

            return (await GetReceiptVoucherAsync(voucher.Id, ct))!;
        }, ct);
    }

    public Task<ReceiptVoucherDto> ReverseReceiptVoucherAsync(int id, ReverseVoucherRequest? request = null, CancellationToken ct = default)
    {
        Require(id > 0, "error.validation.request");

        return posting.RunAsync(async () =>
        {
            await posting.LockAsync($"receipt-voucher:{id}", ct);
            var voucher = await context.ReceiptVouchers
                .Include(v => v.Allocations)
                .FirstOrDefaultAsync(v => v.Id == id, ct)
                ?? throw new BusinessException("error.validation.request", 404);

            Require(voucher.Status != VoucherStatus.Cancelled, "error.treasury.voucher_already_cancelled", 409);
            Require(voucher.Status == VoucherStatus.Posted, "error.treasury.voucher_not_posted", 409);

            var (cashFund, bankAccount, _) = await TreasuryPostingHelper.ResolveAndLockFundAsync(
                context, posting, voucher.PaymentMethod, voucher.CashFundId, voucher.BankAccountId, voucher.BranchId, ct);

            // Reverse the fund credit; throws error.treasury.insufficient_fund_balance if balance is insufficient.
            TreasuryPostingHelper.DebitFundBalance(cashFund, bankAccount, voucher.Amount);

            foreach (var alloc in voucher.Allocations.OrderBy(a => a.SalesInvoiceId))
            {
                await posting.LockAsync($"sales-invoice:{alloc.SalesInvoiceId}", ct);
                var invoice = await context.SalesInvoices
                    .FirstOrDefaultAsync(i => i.Id == alloc.SalesInvoiceId, ct)
                    ?? throw new BusinessException("error.validation.request", 404);

                invoice.RemainingDebt = Round(invoice.RemainingDebt + alloc.AllocatedAmount);
                invoice.PaidAmount = Round(invoice.PaidAmount - alloc.AllocatedAmount);
            }

            if (voucher.PartnerId is > 0 && voucher.ReceiptType is ReceiptType.CustomerPayment or ReceiptType.AdvanceFromCustomer)
            {
                await posting.LockAsync($"partner:{voucher.PartnerId.Value}", ct);
                var partner = await context.Partners.FirstOrDefaultAsync(p => p.Id == voucher.PartnerId.Value, ct);
                if (partner != null)
                {
                    partner.DebtBalance = Round(partner.DebtBalance + voucher.Amount);
                }
            }

            if (voucher.JournalEntryId is > 0)
            {
                var reason = string.IsNullOrWhiteSpace(request?.Reason)
                    ? $"Reverse receipt {voucher.VoucherNumber}"
                    : $"Reverse receipt {voucher.VoucherNumber}: {request.Reason.Trim()}";
                await TreasuryPostingHelper.BuildReversalJournalAsync(
                    context, posting, engine, voucher.JournalEntryId.Value, reason, ct);
            }

            voucher.Status = VoucherStatus.Cancelled;
            if (voucher.Allocations.Count > 0)
            {
                context.AllowCancellation = true;
                try { await context.SaveChangesAsync(ct); }
                finally { context.AllowCancellation = false; }
            }
            else
            {
                await context.SaveChangesAsync(ct);
            }

            return (await GetReceiptVoucherAsync(voucher.Id, ct))!;
        }, ct);
    }

    private async Task<int> ResolveCreditAccountIdAsync(
        CreateReceiptVoucherRequest request,
        PartnerEntity? partner,
        CancellationToken ct)
    {
        var dim = request.PaymentMethod == TreasuryPaymentMethod.Bank ? "BANK" : "CASH";
        return request.ReceiptType switch
        {
            ReceiptType.CustomerPayment or ReceiptType.AdvanceFromCustomer =>
                await engine.ResolveReceivableAccountIdAsync(request.BranchId, partner?.Id, ct),
            ReceiptType.BankWithdrawalToCash =>
                (await engine.ResolvePostingRuleAsync("ACC", "INTERNAL_TRANSFER", request.BranchId, "BANK_TO_CASH", ct)).CreditAccountId,
            ReceiptType.OtherReceipt =>
                (await engine.ResolvePostingRuleAsync("ACC", "RECEIPT_OTHER", request.BranchId, dim, ct)).CreditAccountId,
            _ => throw new BusinessException("error.validation.request", 400)
        };
    }

    private static ReceiptVoucherDto Map(ReceiptVoucher v) => new()
    {
        Id = v.Id,
        VoucherNumber = v.VoucherNumber,
        VoucherDate = v.VoucherDate,
        BranchId = v.BranchId,
        PaymentMethod = v.PaymentMethod,
        CashFundId = v.CashFundId,
        BankAccountId = v.BankAccountId,
        PartnerId = v.PartnerId,
        PayerName = v.PayerName,
        PayerPhone = v.PayerPhone,
        Amount = v.Amount,
        ReceiptType = v.ReceiptType,
        Status = v.Status,
        JournalEntryId = v.JournalEntryId,
        Notes = v.Notes,
        Allocations = v.Allocations.Select(a => new ReceiptAllocationDto
        {
            Id = a.Id,
            SalesInvoiceId = a.SalesInvoiceId,
            InvoiceNumber = a.SalesInvoice?.InvoiceNumber,
            AllocatedAmount = a.AllocatedAmount
        }).ToList()
    };
}
