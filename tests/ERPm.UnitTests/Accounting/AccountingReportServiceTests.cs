using ClosedXML.Excel;
using ERPm.Domain.Entities.Accounting;
using ERPm.Domain.Entities.Partner;
using ERPm.Domain.Entities.Sales;
using ERPm.Domain.Entities.System;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Services.Accounting;
using ERPm.Infrastructure.Services.Common.Transfer;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERPm.UnitTests.Accounting;

public sealed class AccountingReportServiceTests
{
    private static ApplicationDbContext CreateDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task GetArAgingAsync_And_ExportArAgingAsync_CalculateBucketsAndSummary()
    {
        await using var db = CreateDb(nameof(GetArAgingAsync_And_ExportArAgingAsync_CalculateBucketsAndSummary));
        var branch = new Branch { Id = 1, Code = "HN", Name = "Ha Noi", IsActive = true };
        var customer = new Partner { Id = 10, Code = "KH01", Name = "Cong ty A", PartnerType = PartnerType.Customer, IsActive = true };
        db.Branches.Add(branch);
        db.Partners.Add(customer);

        var asOf = new DateTime(2026, 11, 17, 0, 0, 0, DateTimeKind.Utc);
        db.SalesInvoices.AddRange(
            new SalesInvoice
            {
                Id = 1, InvoiceNumber = "HD001", BranchId = 1, WarehouseId = 1, CustomerId = 10, CustomerName = "Cong ty A",
                InvoiceDate = asOf.AddDays(-5), DueDate = asOf.AddDays(10), TotalAmount = 1_000m, PaidAmount = 0m, RemainingDebt = 1_000m, Status = VoucherStatus.Posted
            },
            new SalesInvoice
            {
                Id = 2, InvoiceNumber = "HD002", BranchId = 1, WarehouseId = 1, CustomerId = 10, CustomerName = "Cong ty A",
                InvoiceDate = asOf.AddDays(-25), DueDate = asOf.AddDays(-15), TotalAmount = 2_000m, PaidAmount = 500m, RemainingDebt = 1_500m, Status = VoucherStatus.Posted
            },
            new SalesInvoice
            {
                Id = 3, InvoiceNumber = "HD003", BranchId = 1, WarehouseId = 1, CustomerId = 10, CustomerName = "Cong ty A",
                InvoiceDate = asOf.AddDays(-120), DueDate = asOf.AddDays(-100), TotalAmount = 3_000m, PaidAmount = 0m, RemainingDebt = 3_000m, Status = VoucherStatus.Posted
            },
            new SalesInvoice
            {
                Id = 4, InvoiceNumber = "HD004", BranchId = 1, WarehouseId = 1, CustomerId = 10, CustomerName = "Cong ty A",
                InvoiceDate = asOf.AddDays(-200), DueDate = asOf.AddDays(-150), TotalAmount = 9_999m, PaidAmount = 0m, RemainingDebt = 9_999m, Status = VoucherStatus.Cancelled
            });
        await db.SaveChangesAsync();

        var service = new AccountingReportService(db, new ExcelExportService());
        var report = await service.GetArAgingAsync(asOf, null, null);

        Assert.Equal(1_000m, report.TotalCurrent);
        Assert.Equal(1_500m, report.Total1To30);
        Assert.Equal(0m, report.Total31To60);
        Assert.Equal(0m, report.Total61To90);
        Assert.Equal(3_000m, report.TotalOver90);
        Assert.Equal(5_500m, report.GrandTotal);
        Assert.Single(report.Customers);
        Assert.Equal(1, report.Customers[0].BadDebtInvoiceCount);

        var file = await service.ExportArAgingAsync(asOf, null, null);
        Assert.Equal("ar-aging-20261117.xlsx", file.FileName);
        using var ms = new MemoryStream(file.Content);
        using var wb = new XLWorkbook(ms);
        var ws = wb.Worksheet("AR Aging");
        Assert.Equal(5_500m, ws.Cell(6, 7).GetValue<decimal>());
    }

    [Fact]
    public async Task Statement_GeneralJournal_GeneralLedger_And_TreasuryLedger_AreConsistent()
    {
        await using var db = CreateDb(nameof(Statement_GeneralJournal_GeneralLedger_And_TreasuryLedger_AreConsistent));
        var branch = new Branch { Id = 1, Code = "HN", Name = "Ha Noi", IsActive = true };
        var customer = new Partner { Id = 20, Code = "KH02", Name = "Khach B", PartnerType = PartnerType.Customer, IsActive = true };
        var acc111 = new Account { Id = 1, Code = "1111", Name = "Tien mat", AccountType = AccountType.Asset, Nature = AccountNature.DebitOnly, IsPostable = true };
        var acc131 = new Account { Id = 2, Code = "131", Name = "Phai thu KH", AccountType = AccountType.Asset, Nature = AccountNature.Bilateral, IsPostable = true };
        var acc511 = new Account { Id = 3, Code = "511", Name = "Doanh thu", AccountType = AccountType.Revenue, Nature = AccountNature.CreditOnly, IsPostable = true };
        var cashFund = new CashFund { Id = 5, Code = "CF1", Name = "Quy chinh", BranchId = 1, AccountId = 1, CurrentBalance = 1_400m, IsActive = true };

        db.Branches.Add(branch);
        db.Partners.Add(customer);
        db.Accounts.AddRange(acc111, acc131, acc511);
        db.CashFunds.Add(cashFund);

        var t0 = new DateTime(2026, 11, 1, 8, 0, 0, DateTimeKind.Utc);
        var t1 = new DateTime(2026, 11, 10, 8, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 11, 12, 8, 0, 0, DateTimeKind.Utc);

        db.SalesInvoices.AddRange(
            new SalesInvoice
            {
                Id = 1, InvoiceNumber = "HD-OLD", BranchId = 1, WarehouseId = 1, CustomerId = 20, CustomerName = "Khach B",
                InvoiceDate = t0.AddDays(-5), PaymentMethod = PaymentMethod.Debt, TotalAmount = 500m, PaidAmount = 0m, RemainingDebt = 500m, Status = VoucherStatus.Posted
            },
            new SalesInvoice
            {
                Id = 2, InvoiceNumber = "HD-NEW", BranchId = 1, WarehouseId = 1, CustomerId = 20, CustomerName = "Khach B",
                InvoiceDate = t1, PaymentMethod = PaymentMethod.Debt, TotalAmount = 1_000m, PaidAmount = 400m, RemainingDebt = 600m, Status = VoucherStatus.Posted
            });

        db.ReceiptVouchers.Add(new ReceiptVoucher
        {
            Id = 1, VoucherNumber = "PT001", VoucherDate = t2, BranchId = 1, PaymentMethod = TreasuryPaymentMethod.Cash,
            CashFundId = 5, PartnerId = 20, PayerName = "Khach B", Amount = 400m, ReceiptType = ReceiptType.CustomerPayment, Status = VoucherStatus.Posted,
            Allocations =
            [
                new ReceiptVoucherAllocation { Id = 1, ReceiptVoucherId = 1, SalesInvoiceId = 2, AllocatedAmount = 400m }
            ]
        });

        db.JournalEntries.AddRange(
            new JournalEntry
            {
                Id = 1, VoucherNumber = "HD-NEW", VoucherType = "SALES_INVOICE", DocDate = t1, PostingDate = t1, BranchId = 1, Status = VoucherStatus.Posted,
                Lines =
                [
                    new JournalEntryLine { Id = 1, AccountId = 2, DebitAmount = 1_000m, CreditAmount = 0m, PartnerId = 20 },
                    new JournalEntryLine { Id = 2, AccountId = 3, DebitAmount = 0m, CreditAmount = 1_000m, PartnerId = 20 }
                ]
            },
            new JournalEntry
            {
                Id = 2, VoucherNumber = "PT001", VoucherType = "RECEIPT", DocDate = t2, PostingDate = t2, BranchId = 1, Status = VoucherStatus.Posted,
                Lines =
                [
                    new JournalEntryLine { Id = 3, AccountId = 1, DebitAmount = 400m, CreditAmount = 0m, PartnerId = 20 },
                    new JournalEntryLine { Id = 4, AccountId = 2, DebitAmount = 0m, CreditAmount = 400m, PartnerId = 20 }
                ]
            });

        await db.SaveChangesAsync();

        var service = new AccountingReportService(db, new ExcelExportService());

        var statement = await service.GetStatementOfAccountAsync(20, t0, t2.AddDays(1), null);
        Assert.Equal(500m, statement.OpeningBalance);
        Assert.Equal(1_000m, statement.TotalDebit);
        Assert.Equal(400m, statement.TotalCredit);
        Assert.Equal(1_100m, statement.ClosingBalance);

        var journal = await service.GetGeneralJournalAsync(t0, t2.AddDays(1), null, null);
        Assert.True(journal.IsBalanced);
        Assert.Equal(1_400m, journal.TotalDebit);
        Assert.Equal(1_400m, journal.TotalCredit);
        Assert.Equal(4, journal.Rows.Count);

        var ledger131 = await service.GetGeneralLedgerAsync(2, t0, t2.AddDays(1), null);
        Assert.Equal(1_000m, ledger131.TotalDebit);
        Assert.Equal(400m, ledger131.TotalCredit);
        Assert.Equal(600m, ledger131.ClosingBalance);

        var treasuryLedger = await service.GetTreasuryLedgerAsync(TreasuryPaymentMethod.Cash, 5, t0, t2.AddDays(1));
        Assert.Equal(1_000m, treasuryLedger.OpeningBalance);
        Assert.Equal(400m, treasuryLedger.TotalIn);
        Assert.Equal(0m, treasuryLedger.TotalOut);
        Assert.Equal(1_400m, treasuryLedger.ClosingBalance);
    }
}
