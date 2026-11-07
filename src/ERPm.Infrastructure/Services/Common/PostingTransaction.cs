using System.Security.Cryptography;
using System.Text.Json;
using ERPm.Application.Common;
using ERPm.Domain.Entities.Accounting;
using ERPm.Domain.Entities.Sales;
using ERPm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERPm.Infrastructure.Services.Common;

public sealed class PostingTransaction(ApplicationDbContext context)
{
    public Task<T> RunAsync<T>(Func<Task<T>> action, CancellationToken ct) =>
        context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            // Rebuild tracked state on a retry, including an uncertain commit outcome.
            context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync(ct);
            var result = await action();
            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
        });

    public Task LockAsync(string key, CancellationToken ct) =>
        context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);

    public Task LockSharedAsync(string key, CancellationToken ct) =>
        context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock_shared(hashtextextended({key}, 0))", ct);

    /// <summary>
    /// Receivables of a customer: unpaid posted invoices minus posted sales returns settled against the debt.
    /// </summary>
    public async Task<decimal> ReceivablesAsync(int customerId, CancellationToken ct)
    {
        var invoiced = await context.SalesInvoices.IgnoreQueryFilters()
            .Where(i => !i.IsDeleted && i.Status == VoucherStatus.Posted && i.CustomerId == customerId).SumAsync(i => i.RemainingDebt, ct);
        var credited = await context.SalesReturns.IgnoreQueryFilters()
            .Where(r => !r.IsDeleted && r.Status == VoucherStatus.Posted && r.CustomerId == customerId && r.RefundMethod == RefundMethod.ReduceDebt).SumAsync(r => r.TotalRefundAmount, ct);
        return invoiced - credited;
    }

    /// <summary>Next value of the shared document number sequence.</summary>
    public Task<long> NextValueAsync(CancellationToken ct) =>
        context.Database.SqlQueryRaw<long>("SELECT nextval('erpm_document_number') AS \"Value\"").SingleAsync(ct);

    public async Task<string> NumberAsync(string prefix, CancellationToken ct) => $"{prefix}{await NextValueAsync(ct):D10}";

    public static string Hash<T>(T request) => Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request)));
    public static decimal Round(decimal value) => decimal.Round(value, 4, MidpointRounding.AwayFromZero);

    public static void Require(bool condition, string code, int status = 400)
    {
        if (!condition) throw new BusinessException(code, status);
    }

    public static void ValidateNumber(decimal value, bool positive = false)
    {
        Require(value >= 0 && (!positive || value > 0) && value <= 99999999999999.9999m && Round(value) == value, "error.validation.number");
    }

    public async Task<Dictionary<string, int>> AccountsAsync(IEnumerable<string> codes, CancellationToken ct)
    {
        var required = codes.Distinct().ToArray();
        var accounts = await context.Accounts.Where(a => a.IsActive && required.Contains(a.Code))
            .ToDictionaryAsync(a => a.Code, a => a.Id, ct);
        Require(accounts.Count == required.Length, "error.accounting.accounts_missing", 409);
        return accounts;
    }

    public static void ValidateJournal(JournalEntry journal)
    {
        Require(journal.Lines.Count >= 2 && journal.Lines.All(l => l.AccountId > 0 && l.DebitAmount >= 0 && l.CreditAmount >= 0 &&
            (l.DebitAmount == 0) != (l.CreditAmount == 0)) &&
            journal.Lines.Sum(l => l.DebitAmount) == journal.Lines.Sum(l => l.CreditAmount), "error.accounting.unbalanced", 409);
    }
}
