using ERPm.Application.Common;
using ERPm.Application.Common.Transfer;
using ERPm.Application.DTOs.Accounting;
using ERPm.Application.Features.Accounting;
using ERPm.Domain.Entities.Accounting;
using ERPm.Domain.Entities.Sales;
using ERPm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERPm.Infrastructure.Services.Accounting;

public sealed class AccountingReportService(
    ApplicationDbContext db,
    IExcelExportService excel) : IAccountingReportService
{
    public static string ClassifyAgingBucket(int daysOverdue) => daysOverdue switch
    {
        <= 0 => "Current",
        <= 30 => "1-30",
        <= 60 => "31-60",
        <= 90 => "61-90",
        _ => ">90"
    };

    public async Task<ArAgingReportDto> GetArAgingAsync(
        DateTime? asOfDate,
        int? branchId,
        int? customerId,
        CancellationToken ct = default)
    {
        var cutoff = NormalizeUtcDate(asOfDate ?? DateTime.UtcNow).Date;

        var query = db.SalesInvoices.AsNoTracking()
            .Where(i => i.Status == VoucherStatus.Posted && i.RemainingDebt > 0m);

        if (branchId.HasValue) query = query.Where(i => i.BranchId == branchId.Value);
        if (customerId.HasValue) query = query.Where(i => i.CustomerId == customerId.Value);

        var rawInvoices = await query
            .Select(i => new
            {
                i.Id,
                i.InvoiceNumber,
                i.InvoiceDate,
                i.DueDate,
                i.CustomerId,
                i.CustomerName,
                i.BranchId,
                BranchName = i.Branch != null ? i.Branch.Name : string.Empty,
                i.TotalAmount,
                i.PaidAmount,
                i.RemainingDebt
            })
            .OrderBy(i => i.DueDate ?? i.InvoiceDate)
            .ThenBy(i => i.Id)
            .ToListAsync(ct);

        var items = rawInvoices.Select(i =>
        {
            var due = (i.DueDate ?? i.InvoiceDate).Date;
            var daysOverdue = Math.Max(0, (cutoff - due).Days);
            return new ArAgingInvoiceDto
            {
                InvoiceId = i.Id,
                InvoiceNumber = i.InvoiceNumber,
                InvoiceDate = i.InvoiceDate,
                DueDate = i.DueDate ?? i.InvoiceDate,
                CustomerId = i.CustomerId,
                CustomerName = i.CustomerName,
                BranchId = i.BranchId,
                BranchName = i.BranchName,
                TotalAmount = i.TotalAmount,
                PaidAmount = i.PaidAmount,
                RemainingDebt = i.RemainingDebt,
                DaysOverdue = daysOverdue,
                AgingBucket = ClassifyAgingBucket(daysOverdue),
                IsBadDebtWarning = daysOverdue > 90
            };
        }).ToList();

        var customers = items
            .GroupBy(i => new { i.CustomerId, i.CustomerName })
            .Select(g => new ArAgingCustomerSummaryDto
            {
                CustomerId = g.Key.CustomerId,
                CustomerName = g.Key.CustomerName,
                Current = g.Where(x => x.AgingBucket == "Current").Sum(x => x.RemainingDebt),
                Days1To30 = g.Where(x => x.AgingBucket == "1-30").Sum(x => x.RemainingDebt),
                Days31To60 = g.Where(x => x.AgingBucket == "31-60").Sum(x => x.RemainingDebt),
                Days61To90 = g.Where(x => x.AgingBucket == "61-90").Sum(x => x.RemainingDebt),
                Over90Days = g.Where(x => x.AgingBucket == ">90").Sum(x => x.RemainingDebt),
                TotalRemainingDebt = g.Sum(x => x.RemainingDebt),
                BadDebtInvoiceCount = g.Count(x => x.IsBadDebtWarning)
            })
            .OrderByDescending(c => c.TotalRemainingDebt)
            .ToList();

        return new ArAgingReportDto
        {
            AsOfDate = cutoff,
            TotalCurrent = customers.Sum(c => c.Current),
            Total1To30 = customers.Sum(c => c.Days1To30),
            Total31To60 = customers.Sum(c => c.Days31To60),
            Total61To90 = customers.Sum(c => c.Days61To90),
            TotalOver90 = customers.Sum(c => c.Over90Days),
            GrandTotal = customers.Sum(c => c.TotalRemainingDebt),
            Customers = customers,
            Invoices = items
        };
    }

    public async Task<ExportFileResult> ExportArAgingAsync(
        DateTime? asOfDate,
        int? branchId,
        int? customerId,
        CancellationToken ct = default)
    {
        var report = await GetArAgingAsync(asOfDate, branchId, customerId, ct);
        return excel.Export(new ExcelExportRequest<ArAgingCustomerSummaryDto>
        {
            SheetName = "AR Aging",
            Title = "Báo cáo Phân tích Tuổi nợ Khách hàng (AR Aging)",
            Subtitle = $"Tính đến ngày: {report.AsOfDate:yyyy-MM-dd}",
            Columns =
            [
                new ExportColumn<ArAgingCustomerSummaryDto> { Header = "Khách hàng", ValueSelector = x => x.CustomerName },
                new ExportColumn<ArAgingCustomerSummaryDto> { Header = "Chưa đến hạn", ValueSelector = x => x.Current, NumberFormat = "#,##0", Alignment = ExportAlignment.Right },
                new ExportColumn<ArAgingCustomerSummaryDto> { Header = "Quá hạn 1-30 ngày", ValueSelector = x => x.Days1To30, NumberFormat = "#,##0", Alignment = ExportAlignment.Right },
                new ExportColumn<ArAgingCustomerSummaryDto> { Header = "Quá hạn 31-60 ngày", ValueSelector = x => x.Days31To60, NumberFormat = "#,##0", Alignment = ExportAlignment.Right },
                new ExportColumn<ArAgingCustomerSummaryDto> { Header = "Quá hạn 61-90 ngày", ValueSelector = x => x.Days61To90, NumberFormat = "#,##0", Alignment = ExportAlignment.Right },
                new ExportColumn<ArAgingCustomerSummaryDto> { Header = "Quá hạn >90 ngày (Nợ xấu)", ValueSelector = x => x.Over90Days, NumberFormat = "#,##0", Alignment = ExportAlignment.Right },
                new ExportColumn<ArAgingCustomerSummaryDto> { Header = "Tổng dư nợ", ValueSelector = x => x.TotalRemainingDebt, NumberFormat = "#,##0", Alignment = ExportAlignment.Right },
                new ExportColumn<ArAgingCustomerSummaryDto> { Header = "Số HĐ >90 ngày", ValueSelector = x => x.BadDebtInvoiceCount, Alignment = ExportAlignment.Center }
            ],
            Rows = report.Customers,
            SummaryValues = new Dictionary<int, object?>
            {
                [0] = "Tổng cộng",
                [1] = report.TotalCurrent,
                [2] = report.Total1To30,
                [3] = report.Total31To60,
                [4] = report.Total61To90,
                [5] = report.TotalOver90,
                [6] = report.GrandTotal
            }
        }, $"ar-aging-{report.AsOfDate:yyyyMMdd}");
    }

    public async Task<StatementOfAccountDto> GetStatementOfAccountAsync(
        int partnerId,
        DateTime? from,
        DateTime? to,
        int? branchId,
        CancellationToken ct = default)
    {
        var partner = await db.Partners.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == partnerId, ct)
            ?? throw new BusinessException("error.partner.not_found", 404);

        var (fromUtc, toUtc) = NormalizeRange(from, to);

        var invoiceQuery = db.SalesInvoices.AsNoTracking()
            .Where(i => i.CustomerId == partnerId && i.Status == VoucherStatus.Posted);
        var receiptQuery = db.ReceiptVouchers.AsNoTracking()
            .Where(r => r.PartnerId == partnerId && r.Status == VoucherStatus.Posted &&
                        (r.ReceiptType == ReceiptType.CustomerPayment || r.ReceiptType == ReceiptType.AdvanceFromCustomer));
        var returnQuery = db.SalesReturns.AsNoTracking()
            .Where(sr => sr.CustomerId == partnerId && sr.Status == VoucherStatus.Posted);

        if (branchId.HasValue)
        {
            invoiceQuery = invoiceQuery.Where(i => i.BranchId == branchId.Value);
            receiptQuery = receiptQuery.Where(r => r.BranchId == branchId.Value);
            returnQuery = returnQuery.Where(sr => sr.BranchId == branchId.Value);
        }

        var invoices = await invoiceQuery
            .Select(i => new { i.InvoiceDate, i.InvoiceNumber, i.TotalAmount, i.PaymentMethod, i.PaidAmount, i.Id })
            .ToListAsync(ct);
        var allocatedByInvoice = await db.ReceiptVoucherAllocations.AsNoTracking()
            .Where(a => a.ReceiptVoucher != null && a.ReceiptVoucher.Status == VoucherStatus.Posted && a.ReceiptVoucher.PartnerId == partnerId)
            .GroupBy(a => a.SalesInvoiceId)
            .Select(g => new { InvoiceId = g.Key, Allocated = g.Sum(x => x.AllocatedAmount) })
            .ToDictionaryAsync(x => x.InvoiceId, x => x.Allocated, ct);

        var receipts = await receiptQuery
            .Select(r => new { r.VoucherDate, r.VoucherNumber, r.Amount, r.Notes })
            .ToListAsync(ct);
        var returns = await returnQuery
            .Select(sr => new { sr.ReturnDate, sr.ReturnNumber, sr.TotalRefundAmount, sr.Notes })
            .ToListAsync(ct);

        var allTx = new List<(DateTime Date, string DocNo, string DocType, string? Desc, decimal Debit, decimal Credit)>();

        foreach (var inv in invoices)
        {
            allTx.Add((inv.InvoiceDate, inv.InvoiceNumber, "INVOICE", $"Hóa đơn bán hàng {inv.InvoiceNumber}", inv.TotalAmount, 0m));
            // Immediate cash/bank tender at POS (excluding amounts settled by ReceiptVouchers)
            var allocated = allocatedByInvoice.GetValueOrDefault(inv.Id, 0m);
            var immediatePaid = Math.Max(0m, inv.PaidAmount - allocated);
            if (immediatePaid > 0m)
            {
                allTx.Add((inv.InvoiceDate, inv.InvoiceNumber, "POS_PAYMENT", $"Thanh toán trực tiếp {inv.InvoiceNumber}", 0m, immediatePaid));
            }
        }

        foreach (var rc in receipts)
        {
            allTx.Add((rc.VoucherDate, rc.VoucherNumber, "RECEIPT", rc.Notes ?? $"Phiếu thu {rc.VoucherNumber}", 0m, rc.Amount));
        }

        foreach (var ret in returns)
        {
            allTx.Add((ret.ReturnDate, ret.ReturnNumber, "SALES_RETURN", ret.Notes ?? $"Trả hàng {ret.ReturnNumber}", 0m, ret.TotalRefundAmount));
        }

        var openingBalance = allTx
            .Where(t => t.Date < fromUtc)
            .Sum(t => t.Debit - t.Credit);

        var periodTx = allTx
            .Where(t => t.Date >= fromUtc && t.Date <= toUtc)
            .OrderBy(t => t.Date)
            .ThenBy(t => t.DocNo, StringComparer.Ordinal)
            .ToList();

        var running = openingBalance;
        var rows = new List<StatementTransactionDto>(periodTx.Count);
        foreach (var tx in periodTx)
        {
            running += tx.Debit - tx.Credit;
            rows.Add(new StatementTransactionDto
            {
                TransactionDate = tx.Date,
                DocumentNumber = tx.DocNo,
                DocumentType = tx.DocType,
                Description = tx.Desc,
                DebitAmount = tx.Debit,
                CreditAmount = tx.Credit,
                RunningBalance = running
            });
        }

        return new StatementOfAccountDto
        {
            PartnerId = partner.Id,
            PartnerCode = partner.Code,
            PartnerName = partner.Name,
            FromDate = fromUtc,
            ToDate = toUtc,
            OpeningBalance = openingBalance,
            TotalDebit = rows.Sum(r => r.DebitAmount),
            TotalCredit = rows.Sum(r => r.CreditAmount),
            ClosingBalance = running,
            Transactions = rows
        };
    }

    public async Task<GeneralJournalReportDto> GetGeneralJournalAsync(
        DateTime? from,
        DateTime? to,
        int? branchId,
        string? voucherType,
        CancellationToken ct = default)
    {
        var (fromUtc, toUtc) = NormalizeRange(from, to);

        var query = db.JournalEntryLines.AsNoTracking()
            .Where(l => l.JournalEntry != null &&
                        l.JournalEntry.Status == VoucherStatus.Posted &&
                        l.JournalEntry.PostingDate >= fromUtc &&
                        l.JournalEntry.PostingDate <= toUtc);

        if (branchId.HasValue)
        {
            query = query.Where(l => l.JournalEntry!.BranchId == branchId.Value);
        }
        if (!string.IsNullOrWhiteSpace(voucherType))
        {
            var vt = voucherType.Trim();
            query = query.Where(l => l.JournalEntry!.VoucherType == vt);
        }

        var rows = await query
            .OrderBy(l => l.JournalEntry!.PostingDate)
            .ThenBy(l => l.JournalEntryId)
            .ThenBy(l => l.Id)
            .Select(l => new GeneralJournalRowDto
            {
                JournalEntryId = l.JournalEntryId,
                PostingDate = l.JournalEntry!.PostingDate,
                DocDate = l.JournalEntry.DocDate,
                VoucherNumber = l.JournalEntry.VoucherNumber,
                VoucherType = l.JournalEntry.VoucherType,
                BranchId = l.JournalEntry.BranchId,
                BranchName = l.JournalEntry.Branch != null ? l.JournalEntry.Branch.Name : string.Empty,
                AccountCode = l.Account != null ? l.Account.Code : string.Empty,
                AccountName = l.Account != null ? l.Account.Name : string.Empty,
                PartnerName = l.Partner != null ? l.Partner.Name : null,
                Description = l.Description ?? l.JournalEntry.Description,
                DebitAmount = l.DebitAmount,
                CreditAmount = l.CreditAmount
            })
            .ToListAsync(ct);

        return new GeneralJournalReportDto
        {
            FromDate = fromUtc,
            ToDate = toUtc,
            TotalDebit = rows.Sum(r => r.DebitAmount),
            TotalCredit = rows.Sum(r => r.CreditAmount),
            Rows = rows
        };
    }

    public async Task<GeneralLedgerReportDto> GetGeneralLedgerAsync(
        int accountId,
        DateTime? from,
        DateTime? to,
        int? branchId,
        CancellationToken ct = default)
    {
        var account = await db.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountId, ct)
            ?? throw new BusinessException("error.accounting.account_not_found", 404);

        var (fromUtc, toUtc) = NormalizeRange(from, to);

        var baseQuery = db.JournalEntryLines.AsNoTracking()
            .Where(l => l.AccountId == accountId &&
                        l.JournalEntry != null &&
                        l.JournalEntry.Status == VoucherStatus.Posted);

        if (branchId.HasValue)
        {
            baseQuery = baseQuery.Where(l => l.JournalEntry!.BranchId == branchId.Value);
        }

        var sign = account.AccountType is AccountType.Asset or AccountType.Expense ? 1m : -1m;

        var openingAgg = await baseQuery
            .Where(l => l.JournalEntry!.PostingDate < fromUtc)
            .GroupBy(_ => 1)
            .Select(g => new { Debit = g.Sum(x => x.DebitAmount), Credit = g.Sum(x => x.CreditAmount) })
            .FirstOrDefaultAsync(ct);

        var openingBalance = openingAgg is null ? 0m : (openingAgg.Debit - openingAgg.Credit) * sign;

        var periodLines = await baseQuery
            .Where(l => l.JournalEntry!.PostingDate >= fromUtc && l.JournalEntry.PostingDate <= toUtc)
            .OrderBy(l => l.JournalEntry!.PostingDate)
            .ThenBy(l => l.JournalEntryId)
            .ThenBy(l => l.Id)
            .Select(l => new
            {
                l.JournalEntry!.PostingDate,
                l.JournalEntry.VoucherNumber,
                l.JournalEntry.VoucherType,
                Description = l.Description ?? l.JournalEntry.Description,
                l.DebitAmount,
                l.CreditAmount
            })
            .ToListAsync(ct);

        var running = openingBalance;
        var rows = new List<GeneralLedgerRowDto>(periodLines.Count);
        foreach (var l in periodLines)
        {
            running += (l.DebitAmount - l.CreditAmount) * sign;
            rows.Add(new GeneralLedgerRowDto
            {
                PostingDate = l.PostingDate,
                VoucherNumber = l.VoucherNumber,
                VoucherType = l.VoucherType,
                Description = l.Description,
                DebitAmount = l.DebitAmount,
                CreditAmount = l.CreditAmount,
                RunningBalance = running
            });
        }

        return new GeneralLedgerReportDto
        {
            AccountId = account.Id,
            AccountCode = account.Code,
            AccountName = account.Name,
            FromDate = fromUtc,
            ToDate = toUtc,
            OpeningBalance = openingBalance,
            TotalDebit = rows.Sum(r => r.DebitAmount),
            TotalCredit = rows.Sum(r => r.CreditAmount),
            ClosingBalance = running,
            Rows = rows
        };
    }

    public async Task<TreasuryLedgerReportDto> GetTreasuryLedgerAsync(
        TreasuryPaymentMethod method,
        int fundOrBankId,
        DateTime? from,
        DateTime? to,
        CancellationToken ct = default)
    {
        string fundName;
        decimal currentBalance;
        if (method == TreasuryPaymentMethod.Cash)
        {
            var fund = await db.CashFunds.AsNoTracking().FirstOrDefaultAsync(f => f.Id == fundOrBankId, ct)
                ?? throw new BusinessException("error.treasury.cash_fund_not_found", 404);
            fundName = fund.Name;
            currentBalance = fund.CurrentBalance;
        }
        else
        {
            var bank = await db.BankAccounts.AsNoTracking().FirstOrDefaultAsync(b => b.Id == fundOrBankId, ct)
                ?? throw new BusinessException("error.treasury.bank_account_not_found", 404);
            fundName = $"{bank.BankName} - {bank.AccountNumber}";
            currentBalance = bank.CurrentBalance;
        }

        var (fromUtc, toUtc) = NormalizeRange(from, to);

        var receipts = await db.ReceiptVouchers.AsNoTracking()
            .Where(r => r.Status == VoucherStatus.Posted &&
                        r.PaymentMethod == method &&
                        (method == TreasuryPaymentMethod.Cash ? r.CashFundId == fundOrBankId : r.BankAccountId == fundOrBankId))
            .Select(r => new { Date = r.VoucherDate, No = r.VoucherNumber, Direction = "RECEIPT", Name = r.PayerName, Desc = r.Notes, In = r.Amount, Out = 0m })
            .ToListAsync(ct);

        var payments = await db.PaymentVouchers.AsNoTracking()
            .Where(p => p.Status == VoucherStatus.Posted &&
                        p.PaymentMethod == method &&
                        (method == TreasuryPaymentMethod.Cash ? p.CashFundId == fundOrBankId : p.BankAccountId == fundOrBankId))
            .Select(p => new { Date = p.VoucherDate, No = p.VoucherNumber, Direction = "PAYMENT", Name = p.RecipientName, Desc = p.Notes, In = 0m, Out = p.Amount })
            .ToListAsync(ct);

        var all = receipts.Concat(payments).ToList();
        var opening = currentBalance - all.Where(x => x.Date >= fromUtc).Sum(x => x.In - x.Out);
        var period = all.Where(x => x.Date >= fromUtc && x.Date <= toUtc)
            .OrderBy(x => x.Date)
            .ThenBy(x => x.No, StringComparer.Ordinal)
            .ToList();

        var running = opening;
        var rows = new List<TreasuryLedgerRowDto>(period.Count);
        foreach (var tx in period)
        {
            running += tx.In - tx.Out;
            rows.Add(new TreasuryLedgerRowDto
            {
                VoucherDate = tx.Date,
                VoucherNumber = tx.No,
                Direction = tx.Direction,
                CounterpartyName = tx.Name,
                Description = tx.Desc,
                CashIn = tx.In,
                CashOut = tx.Out,
                RunningBalance = running
            });
        }

        return new TreasuryLedgerReportDto
        {
            PaymentMethod = method,
            FundOrBankId = fundOrBankId,
            FundOrBankName = fundName,
            FromDate = fromUtc,
            ToDate = toUtc,
            OpeningBalance = opening,
            TotalIn = rows.Sum(r => r.CashIn),
            TotalOut = rows.Sum(r => r.CashOut),
            ClosingBalance = running,
            Rows = rows
        };
    }

    private static DateTime NormalizeUtcDate(DateTime dt) =>
        dt.Kind == DateTimeKind.Utc ? dt : DateTime.SpecifyKind(dt, DateTimeKind.Utc);

    private static (DateTime FromUtc, DateTime ToUtc) NormalizeRange(DateTime? from, DateTime? to)
    {
        var now = DateTime.UtcNow;
        var start = from.HasValue
            ? NormalizeUtcDate(from.Value).Date
            : new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = to.HasValue
            ? NormalizeUtcDate(to.Value).Date.AddDays(1).AddTicks(-1)
            : now;
        return (start, end);
    }
}
