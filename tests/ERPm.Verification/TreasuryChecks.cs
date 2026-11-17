using ERPm.Application.Common.Interfaces;
using ERPm.Application.DTOs.Accounting;
using ERPm.Application.DTOs.Sales;
using ERPm.Domain.Entities.Accounting;
using ERPm.Domain.Entities.Catalog;
using ERPm.Domain.Entities.Inventory;
using ERPm.Domain.Entities.Partner;
using ERPm.Domain.Entities.Sales;
using ERPm.Domain.Entities.System;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Services;
using ERPm.Infrastructure.Services.Accounting;
using ERPm.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace ERPm.Verification;

public static class TreasuryChecks
{
    public sealed record Context(
        Func<ICurrentUser?, ApplicationDbContext> Db,
        Action<bool, string> Check,
        Func<Func<Task>, string, Task> Reject);

    public static async Task RunDbChecksAsync(Context ctx)
    {
        await using var db = ctx.Db(new TestUser());
        var branches = new BranchService(db);
        var posting = new PostingTransaction(db);
        var engine = new DynamicPostingEngine(db);
        var treasury = new TreasuryFundService(db, branches, posting, engine);
        var receipts = new ReceiptVoucherService(db, branches, posting, engine);
        var payments = new PaymentVoucherService(db, branches, posting, engine);

        var rootBranch = await db.Branches.FirstAsync(b => b.Code == "root");
        var branch = new Branch { Code = "TRE26", Name = "Treasury Branch", ParentId = rootBranch.Id, IsActive = true };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        var acc1111 = await db.Accounts.FirstAsync(a => a.Code == "1111");
        var acc1121 = await db.Accounts.FirstAsync(a => a.Code == "1121");
        var acc141 = await db.Accounts.FirstAsync(a => a.Code == "141");

        // 1. Create CashFund & BankAccount and check balances
        var cashFund = await treasury.CreateCashFundAsync(new CreateCashFundRequest
        {
            Code = "CF-TRE26",
            Name = "Main Cash TRE26",
            BranchId = branch.Id,
            AccountId = acc1111.Id,
            OpeningBalance = 1_000_000m
        });
        var bankAccount = await treasury.CreateBankAccountAsync(new CreateBankAccountRequest
        {
            BankName = "Vietcombank",
            AccountNumber = "001100999888",
            AccountHolder = "ERPm Corp",
            BranchId = branch.Id,
            AccountId = acc1121.Id,
            OpeningBalance = 500_000m
        });

        var summary = await treasury.GetBalancesAsync(branch.Id);
        ctx.Check(summary.TotalCashBalance == 1_000_000m && summary.TotalBankBalance == 500_000m && summary.TotalBalance == 1_500_000m,
            "Treasury balance summary aggregates cash and bank balances");

        // 2. Internal transfer: Cash -> Bank (300,000) and insufficient balance rejection
        var transfer = await treasury.TransferInternalAsync(new InternalTransferRequest
        {
            BranchId = branch.Id,
            Direction = InternalTransferDirection.CashToBank,
            CashFundId = cashFund.Id,
            BankAccountId = bankAccount.Id,
            Amount = 300_000m,
            Notes = "Deposit cash to bank"
        });
        ctx.Check(transfer.CashFundBalance == 700_000m && transfer.BankAccountBalance == 800_000m,
            "Internal transfer updates both cash and bank balances");

        await ctx.Reject(
            () => treasury.TransferInternalAsync(new InternalTransferRequest
            {
                BranchId = branch.Id,
                Direction = InternalTransferDirection.CashToBank,
                CashFundId = cashFund.Id,
                BankAccountId = bankAccount.Id,
                Amount = 999_999m
            }),
            "error.treasury.insufficient_fund_balance");

        // 3. Setup Customer + 2 Credit SalesInvoices for Allocation testing
        var partner = new Partner
        {
            Code = "KH-TRE26",
            Name = "Treasury Customer",
            PartnerType = PartnerType.Customer,
            IsUnlimitedCredit = true,
            IsActive = true
        };
        var warehouse = new Warehouse { Code = "WH-TRE26", Name = "TRE26 WH", BranchId = branch.Id, IsActive = true };
        var uom = await db.UnitsOfMeasure.FirstAsync();
        db.Partners.Add(partner);
        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync();

        var item = new Item
        {
            Code = "ITEM-TRE26",
            Name = "TRE26 Item",
            ItemType = ItemType.Goods,
            BaseUomId = uom.Id,
            SalePrice = 100_000m,
            IsActive = true
        };
        db.Items.Add(item);
        await db.SaveChangesAsync();
        db.StockBalances.Add(new StockBalance
        {
            WarehouseId = warehouse.Id,
            ItemId = item.Id,
            QuantityOnHand = 100m,
            AverageCostPrice = 50_000m,
            InventoryValue = 5_000_000m
        });
        await db.SaveChangesAsync();

        var salesService = new SalesInvoiceService(db, branches, posting, new TestUser(), null, null, engine);
        var inv1 = await salesService.CreateInvoiceAsync(new CreateSalesInvoiceRequest
        {
            RequestId = Guid.NewGuid(),
            BranchId = branch.Id,
            WarehouseId = warehouse.Id,
            CustomerId = partner.Id,
            PaymentMethod = PaymentMethod.Debt,
            PaidAmount = 0m,
            Items = [new() { ItemId = item.Id, Quantity = 2m, UnitPrice = 100_000m }]
        });
        var inv2 = await salesService.CreateInvoiceAsync(new CreateSalesInvoiceRequest
        {
            RequestId = Guid.NewGuid(),
            BranchId = branch.Id,
            WarehouseId = warehouse.Id,
            CustomerId = partner.Id,
            PaymentMethod = PaymentMethod.Debt,
            PaidAmount = 0m,
            Items = [new() { ItemId = item.Id, Quantity = 3m, UnitPrice = 100_000m }]
        });
        // inv1 = 200,000; inv2 = 300,000; partner.DebtBalance = 500,000
        var partnerEntity = await db.Partners.AsNoTracking().FirstAsync(p => p.Id == partner.Id);
        ctx.Check(partnerEntity.DebtBalance == 500_000m, "Partner debt balance starts at 500,000 from 2 credit invoices");

        // 4. Allocation validations
        await ctx.Reject(
            () => receipts.CreateReceiptVoucherAsync(new CreateReceiptVoucherRequest
            {
                BranchId = branch.Id,
                PaymentMethod = TreasuryPaymentMethod.Cash,
                CashFundId = cashFund.Id,
                PartnerId = partner.Id,
                PayerName = partner.Name,
                Amount = 100_000m,
                ReceiptType = ReceiptType.CustomerPayment,
                Allocations = [new() { SalesInvoiceId = inv1.Id, AllocatedAmount = 150_000m }]
            }),
            "error.treasury.allocation_exceeds_amount");

        await ctx.Reject(
            () => receipts.CreateReceiptVoucherAsync(new CreateReceiptVoucherRequest
            {
                BranchId = branch.Id,
                PaymentMethod = TreasuryPaymentMethod.Cash,
                CashFundId = cashFund.Id,
                PartnerId = partner.Id,
                PayerName = partner.Name,
                Amount = 300_000m,
                ReceiptType = ReceiptType.CustomerPayment,
                Allocations = [new() { SalesInvoiceId = inv1.Id, AllocatedAmount = 250_000m }]
            }),
            "error.treasury.allocation_exceeds_debt");

        // 5. Create ReceiptVoucher allocating 200,000 to inv1 (paid in full) and 150,000 to inv2 (partial)
        var receipt = await receipts.CreateReceiptVoucherAsync(new CreateReceiptVoucherRequest
        {
            BranchId = branch.Id,
            PaymentMethod = TreasuryPaymentMethod.Cash,
            CashFundId = cashFund.Id,
            PartnerId = partner.Id,
            PayerName = partner.Name,
            Amount = 350_000m,
            ReceiptType = ReceiptType.CustomerPayment,
            Allocations =
            [
                new() { SalesInvoiceId = inv1.Id, AllocatedAmount = 200_000m },
                new() { SalesInvoiceId = inv2.Id, AllocatedAmount = 150_000m }
            ]
        });

        var dbInv1 = await db.SalesInvoices.AsNoTracking().FirstAsync(i => i.Id == inv1.Id);
        var dbInv2 = await db.SalesInvoices.AsNoTracking().FirstAsync(i => i.Id == inv2.Id);
        partnerEntity = await db.Partners.AsNoTracking().FirstAsync(p => p.Id == partner.Id);
        var cashEntity = await db.CashFunds.AsNoTracking().FirstAsync(f => f.Id == cashFund.Id);

        ctx.Check(dbInv1.RemainingDebt == 0m && dbInv1.PaidAmount == 200_000m, "Invoice 1 is fully paid (RemainingDebt == 0)");
        ctx.Check(dbInv2.RemainingDebt == 150_000m && dbInv2.PaidAmount == 150_000m, "Invoice 2 is partially paid (RemainingDebt == 150,000)");
        ctx.Check(partnerEntity.DebtBalance == 150_000m, "Partner DebtBalance reduced by 350,000 to 150,000");
        ctx.Check(cashEntity.CurrentBalance == 1_050_000m, "CashFund balance increased from 700,000 to 1,050,000");

        // 6. PaymentVoucher: reject over-balance, then pay 900,000 advance (account 141) leaving 150,000
        await ctx.Reject(
            () => payments.CreatePaymentVoucherAsync(new CreatePaymentVoucherRequest
            {
                BranchId = branch.Id,
                PaymentMethod = TreasuryPaymentMethod.Cash,
                CashFundId = cashFund.Id,
                PartnerId = partner.Id,
                RecipientName = "Employee A",
                Amount = 2_000_000m,
                PaymentType = PaymentType.OtherPayment,
                ExpenseAccountId = acc141.Id
            }),
            "error.treasury.insufficient_fund_balance");

        var payment = await payments.CreatePaymentVoucherAsync(new CreatePaymentVoucherRequest
        {
            BranchId = branch.Id,
            PaymentMethod = TreasuryPaymentMethod.Cash,
            CashFundId = cashFund.Id,
            PartnerId = partner.Id,
            RecipientName = "Employee A",
            Amount = 900_000m,
            PaymentType = PaymentType.OtherPayment,
            ExpenseAccountId = acc141.Id
        });
        cashEntity = await db.CashFunds.AsNoTracking().FirstAsync(f => f.Id == cashFund.Id);
        ctx.Check(cashEntity.CurrentBalance == 150_000m, "PaymentVoucher deducts 900,000 leaving 150,000");

        var period = await treasury.GetPeriodSummaryAsync(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow, branch.Id);
        ctx.Check(period.TotalReceipts == 350_000m && period.TotalPayments == 900_000m && period.NetCashFlow == -550_000m,
            "Treasury period summary totals posted receipts and payments of the branch");
        // Dates bound from the query string have Kind=Unspecified; the endpoint must accept them.
        var unspecified = await treasury.GetPeriodSummaryAsync(DateTime.SpecifyKind(DateTime.UtcNow.Date.AddDays(-1), DateTimeKind.Unspecified), DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Unspecified), branch.Id);
        ctx.Check(unspecified.TotalReceipts == 350_000m, "Treasury period summary accepts dates with Kind=Unspecified");
        var emptyPeriod = await treasury.GetPeriodSummaryAsync(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow.AddDays(-20), branch.Id);
        ctx.Check(emptyPeriod.TotalReceipts == 0m && emptyPeriod.TotalPayments == 0m, "Treasury period summary excludes vouchers outside the period");

        // 7. Reversing ReceiptVoucher fails when CashFund (150,000) < Receipt (350,000)
        await ctx.Reject(
            () => receipts.ReverseReceiptVoucherAsync(receipt.Id, new ReverseVoucherRequest { Reason = "Test insufficient" }),
            "error.treasury.insufficient_fund_balance");

        // Reverse PaymentVoucher first -> CashFund restored to 1,050,000
        var reversedPayment = await payments.ReversePaymentVoucherAsync(payment.Id, new ReverseVoucherRequest { Reason = "Cancel advance" });
        ctx.Check(reversedPayment.Status == VoucherStatus.Cancelled, "Reversed PaymentVoucher is marked Cancelled");

        // Now Reverse ReceiptVoucher succeeds -> restores inv1, inv2, partner, cashFund, and creates reversal JournalEntry
        var reversedReceipt = await receipts.ReverseReceiptVoucherAsync(receipt.Id, new ReverseVoucherRequest { Reason = "Bounced check" });
        dbInv1 = await db.SalesInvoices.AsNoTracking().FirstAsync(i => i.Id == inv1.Id);
        dbInv2 = await db.SalesInvoices.AsNoTracking().FirstAsync(i => i.Id == inv2.Id);
        partnerEntity = await db.Partners.AsNoTracking().FirstAsync(p => p.Id == partner.Id);
        cashEntity = await db.CashFunds.AsNoTracking().FirstAsync(f => f.Id == cashFund.Id);
        var reversalJournal = await db.JournalEntries.AsNoTracking()
            .FirstOrDefaultAsync(j => j.ReversesJournalEntryId == receipt.JournalEntryId);

        ctx.Check(reversedReceipt.Status == VoucherStatus.Cancelled, "Reversed ReceiptVoucher is marked Cancelled");
        ctx.Check(dbInv1.RemainingDebt == 200_000m && dbInv2.RemainingDebt == 300_000m, "Reversing receipt restores RemainingDebt on all allocated invoices");
        ctx.Check(partnerEntity.DebtBalance == 500_000m, "Reversing receipt restores Partner DebtBalance");
        ctx.Check(cashEntity.CurrentBalance == 700_000m, "Reversing receipt restores CashFund CurrentBalance");
        ctx.Check(reversalJournal != null, "Reversing receipt creates a reversal JournalEntry with ReversesJournalEntryId");

        // 8. Spec alignment checks: embedded JournalEntry, PagedResult, FIFO allocation, BankToBank, AccountService
        ctx.Check(receipt.JournalEntry != null && receipt.JournalEntry.Lines.Count == 2 && receipt.JournalEntry.TotalDebit == 350_000m,
            "ReceiptVoucherDto embeds JournalEntry with balanced lines");

        var pagedReceipts = await receipts.GetReceiptVouchersAsync(branchId: branch.Id, q: receipt.VoucherNumber, page: 1, pageSize: 10);
        ctx.Check(pagedReceipts.TotalCount == 1 && pagedReceipts.Items.Count == 1, "GetReceiptVouchersAsync supports search q and PagedResult");

        var fifoReceipt = await receipts.CreateReceiptVoucherAsync(new CreateReceiptVoucherRequest
        {
            BranchId = branch.Id,
            PaymentMethod = TreasuryPaymentMethod.Cash,
            CashFundId = cashFund.Id,
            PartnerId = partner.Id,
            PayerName = partner.Name,
            Amount = 250_000m,
            ReceiptType = ReceiptType.CustomerPayment,
            AutoAllocateFifo = true
        });
        ctx.Check(fifoReceipt.Allocations.Count == 2
            && fifoReceipt.Allocations.Any(a => a.SalesInvoiceId == inv1.Id && a.AllocatedAmount == 200_000m)
            && fifoReceipt.Allocations.Any(a => a.SalesInvoiceId == inv2.Id && a.AllocatedAmount == 50_000m),
            "AutoAllocateFifo allocates oldest invoices first");

        var bankAccount2 = await treasury.CreateBankAccountAsync(new CreateBankAccountRequest
        {
            BankName = "Techcombank",
            AccountNumber = $"TCB-{Guid.NewGuid():N}"[..16],
            AccountHolder = "ERPm Verify",
            BranchId = branch.Id,
            AccountId = acc1121.Id,
            OpeningBalance = 0m
        });
        var bankTransfer = await treasury.TransferInternalAsync(new InternalTransferRequest
        {
            BranchId = branch.Id,
            Direction = InternalTransferDirection.BankToBank,
            BankAccountId = bankAccount.Id,
            DestinationBankAccountId = bankAccount2.Id,
            Amount = 100_000m,
            Notes = "Bank to bank transfer"
        });
        ctx.Check(bankTransfer.DestinationBankAccountBalance == 100_000m, "BankToBank transfer credits destination bank account");

        // 4. Outstanding invoices of a customer (S26.6 receipt allocation): right customer, Posted, receivable left, oldest first, branch scope
        var otherBranch = new Branch { Code = "TRE26B", Name = "Treasury Branch B", ParentId = rootBranch.Id, IsActive = true };
        var debtor = new Partner { Code = "KH-TRE26-OPEN", Name = "Open Debt Customer", PartnerType = PartnerType.Customer, IsUnlimitedCredit = true, IsActive = true };
        var bystander = new Partner { Code = "KH-TRE26-OTHER", Name = "Other Customer", PartnerType = PartnerType.Customer, IsUnlimitedCredit = true, IsActive = true };
        db.Branches.Add(otherBranch);
        db.Partners.AddRange(debtor, bystander);
        await db.SaveChangesAsync();
        SalesInvoice Open(string number, int days, int? customerId, int branchId, VoucherStatus status, decimal remaining) => new()
        {
            InvoiceNumber = number, InvoiceDate = DateTime.UtcNow.AddDays(-days), BranchId = branchId, WarehouseId = warehouse.Id, CustomerId = customerId,
            CustomerName = "x", Status = status, TotalAmount = 500m, PaidAmount = 500m - remaining, RemainingDebt = remaining
        };
        db.SalesInvoices.AddRange(
            Open("OPN-NEW", 1, debtor.Id, branch.Id, VoucherStatus.Posted, 40m),
            Open("OPN-OLD", 5, debtor.Id, branch.Id, VoucherStatus.Posted, 100m),
            Open("OPN-PAID", 3, debtor.Id, branch.Id, VoucherStatus.Posted, 0m),
            Open("OPN-VOID", 2, debtor.Id, branch.Id, VoucherStatus.Cancelled, 60m),
            Open("OPN-OTHERCUST", 4, bystander.Id, branch.Id, VoucherStatus.Posted, 70m),
            Open("OPN-OTHERBR", 6, debtor.Id, otherBranch.Id, VoucherStatus.Posted, 30m));
        await db.SaveChangesAsync();
        var openInvoices = await salesService.GetOutstandingInvoicesAsync(debtor.Id);
        ctx.Check(openInvoices.Select(i => i.InvoiceNumber).SequenceEqual(["OPN-OTHERBR", "OPN-OLD", "OPN-NEW"]),
            "Outstanding invoices: only this customer's Posted invoices with a receivable, oldest first");
        await using var scopedDb = ctx.Db(new TestUser(admin: false, branches: [branch.Id]));
        var scopedSales = new SalesInvoiceService(scopedDb, new BranchService(scopedDb), new PostingTransaction(scopedDb), null, null, null, engine);
        ctx.Check((await scopedSales.GetOutstandingInvoicesAsync(debtor.Id)).Select(i => i.InvoiceNumber).SequenceEqual(["OPN-OLD", "OPN-NEW"]),
            "Outstanding invoices respect the branch scope of the user");

        var accountService = new AccountService(db);
        var tree = await accountService.GetAccountsAsync();
        var postable = await accountService.GetAccountsAsync(isPostable: true);
        ctx.Check(tree.Count > 0 && postable.All(a => a.IsPostable), "AccountService returns COA tree and filters postable accounts");

        var createdAccount = await accountService.CreateAccountAsync(new CreateAccountRequest
        {
            Code = $"1119-{Guid.NewGuid():N}"[..10],
            Name = "Temp Cash Subaccount",
            AccountType = AccountType.Asset,
            Nature = AccountNature.DebitOnly,
            IsPostable = true,
            ParentId = acc1111.ParentId
        });
        var updatedAccount = await accountService.UpdateAccountAsync(createdAccount.Id, new UpdateAccountRequest
        {
            Code = createdAccount.Code,
            Name = "Updated Temp Cash Subaccount",
            AccountType = AccountType.Asset,
            Nature = AccountNature.DebitOnly,
            IsPostable = true,
            ParentId = createdAccount.ParentId
        });
        ctx.Check(updatedAccount.Name == "Updated Temp Cash Subaccount", "AccountService updates unused account");

        await ctx.Reject(
            () => accountService.UpdateAccountAsync(acc1111.Id, new UpdateAccountRequest
            {
                Code = acc1111.Code,
                Name = "Should Fail",
                AccountType = acc1111.AccountType,
                Nature = acc1111.Nature,
                IsPostable = acc1111.IsPostable
            }),
            "error.accounting.account_in_use");
    }

    private sealed class TestUser(bool admin = true, int[]? branches = null) : ICurrentUser
    {
        public string? UserId => "1";
        public string? UserName => "treasury-verifier";
        public int? EmployeeId => null;
        public string Role => "Admin";
        public int? CompanyId => null;
        public int? BranchId => null;
        public int? DepartmentId => null;
        public IReadOnlyList<int> AllowedBranchIds => branches ?? [];
        public int[] BranchIds => branches ?? [];
        public bool IsAuthenticated => true;
        public bool IsAdmin => admin;
        public bool IsSystem => false;
    }
}
