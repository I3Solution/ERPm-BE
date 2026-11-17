using ERPm.Application.Common.Interfaces;
using ERPm.Domain.Entities.Accounting;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Services.Accounting;
using ERPm.Infrastructure.Services.Common.Transfer;
using Microsoft.EntityFrameworkCore;

namespace ERPm.Verification;

public static class AccountingReportChecks
{
    public sealed record Context(
        Func<ICurrentUser?, ApplicationDbContext> Db,
        Action<bool, string> Check);

    public static async Task RunDbChecksAsync(Context ctx)
    {
        await using var db = ctx.Db(null);
        var service = new AccountingReportService(db, new ExcelExportService());

        var aging = await service.GetArAgingAsync(DateTime.UtcNow, null, null);
        ctx.Check(aging.GrandTotal >= 0m && aging.Invoices.All(i => i.RemainingDebt > 0m),
            "AR Aging returns only positive remaining debt invoices");

        var export = await service.ExportArAgingAsync(DateTime.UtcNow, null, null);
        ctx.Check(export.Content.Length > 0 && export.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase),
            "AR Aging Excel export generates non-empty .xlsx workbook");

        var journal = await service.GetGeneralJournalAsync(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, null, null);
        ctx.Check(journal.IsBalanced,
            "General Journal total debit equals total credit across all posted entries");

        var acc131 = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Code == "131");
        if (acc131 is not null)
        {
            var ledger = await service.GetGeneralLedgerAsync(acc131.Id, DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, null);
            ctx.Check(ledger.AccountCode == "131",
                "General Ledger resolves Account 131");
        }
    }
}
