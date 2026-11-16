using ERPm.Application.Common;
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

public static class DynamicPostingChecks
{
    public sealed record Context(
        Func<ICurrentUser?, ApplicationDbContext> Db,
        Action<bool, string> Check,
        Func<Func<Task>, string, Task> Reject);

    public static async Task RunDbChecksAsync(Context ctx)
    {
        await using var db = ctx.Db(new TestUser());
        var engine = new DynamicPostingEngine(db);
        var profileService = new PostingProfileService(db);

        // Ensure a branch, warehouse, item, stock, cash fund, bank account, and partner exist
        var branch = await db.Branches.FirstAsync(b => b.Code == "root");
        var subBranch = new Branch { Code = "S26B", Name = "S26 Branch", ParentId = branch.Id, IsActive = true };
        db.Branches.Add(subBranch);
        await db.SaveChangesAsync();

        var acc111 = await db.Accounts.FirstAsync(a => a.Code == "111");
        var acc1111 = await db.Accounts.FirstAsync(a => a.Code == "1111");
        var acc1121 = await db.Accounts.FirstAsync(a => a.Code == "1121");
        var acc131 = await db.Accounts.FirstAsync(a => a.Code == "131");
        var acc511 = await db.Accounts.FirstAsync(a => a.Code == "511");
        var acc3331 = await db.Accounts.FirstAsync(a => a.Code == "3331");

        // Custom postable sub-accounts to verify dynamic overrides
        var acc1112 = new Account { Code = "1112", Name = "Cash Fund 2", AccountType = AccountType.Asset, Nature = AccountNature.DebitOnly, IsPostable = true, ParentId = acc111.Id, IsActive = true };
        var acc1122 = new Account { Code = "1122", Name = "Bank Account 2", AccountType = AccountType.Asset, Nature = AccountNature.DebitOnly, IsPostable = true, IsActive = true };
        var acc1311 = new Account { Code = "1311", Name = "VIP Receivables", AccountType = AccountType.Asset, Nature = AccountNature.Bilateral, IsPostable = true, TrackPartner = true, IsActive = true };
        var acc5111 = new Account { Code = "5111", Name = "Goods Revenue", AccountType = AccountType.Revenue, Nature = AccountNature.CreditOnly, IsPostable = true, IsActive = true };
        db.Accounts.AddRange(acc1112, acc1122, acc1311, acc5111);
        await db.SaveChangesAsync();

        // 1. PostingProfileService: reject non-postable account
        await ctx.Reject(
            () => profileService.UpsertProfileAsync(new UpsertPostingProfileRequest
            {
                Module = "SD",
                TransactionType = "SALES_REVENUE",
                BranchId = subBranch.Id,
                DimensionKey = "CASH",
                DebitAccountId = acc111.Id, // IsPostable = false
                CreditAccountId = acc511.Id
            }),
            "error.accounting.account_not_postable");

        // 2. Global fallback vs branch override
        var globalRule = await engine.ResolvePostingRuleAsync("SD", "SALES_REVENUE", subBranch.Id, "CASH");
        ctx.Check(globalRule.BranchId == null && globalRule.CreditAccountCode == "511", "DynamicPostingEngine falls back to global rule when no branch override exists");

        await profileService.UpsertProfileAsync(new UpsertPostingProfileRequest
        {
            Module = "SD",
            TransactionType = "SALES_REVENUE",
            BranchId = subBranch.Id,
            DimensionKey = "CASH",
            DebitAccountId = acc1112.Id,
            CreditAccountId = acc5111.Id,
            Description = "Branch override for S26B"
        });
        var branchRule = await engine.ResolvePostingRuleAsync("SD", "SALES_REVENUE", subBranch.Id, "CASH");
        ctx.Check(branchRule.BranchId == subBranch.Id && branchRule.CreditAccountCode == "5111", "DynamicPostingEngine prioritizes branch-level posting rule over global default");

        // 3. ValidateJournalAsync checks: unbalanced, non-postable, partner_required
        await ctx.Reject(
            () => engine.ValidateJournalAsync(new JournalEntry
            {
                BranchId = subBranch.Id,
                Lines =
                [
                    new JournalEntryLine { AccountId = acc1111.Id, DebitAmount = 100m, CreditAmount = 0m },
                    new JournalEntryLine { AccountId = acc511.Id, DebitAmount = 0m, CreditAmount = 90m }
                ]
            }),
            "error.accounting.unbalanced_journal");

        await ctx.Reject(
            () => engine.ValidateJournalAsync(new JournalEntry
            {
                BranchId = subBranch.Id,
                Lines =
                [
                    new JournalEntryLine { AccountId = acc111.Id, DebitAmount = 100m, CreditAmount = 0m }, // IsPostable = false
                    new JournalEntryLine { AccountId = acc511.Id, DebitAmount = 0m, CreditAmount = 100m }
                ]
            }),
            "error.accounting.account_not_postable");

        await ctx.Reject(
            () => engine.ValidateJournalAsync(new JournalEntry
            {
                BranchId = subBranch.Id,
                Lines =
                [
                    new JournalEntryLine { AccountId = acc131.Id, DebitAmount = 100m, CreditAmount = 0m, PartnerId = null }, // TrackPartner = true
                    new JournalEntryLine { AccountId = acc511.Id, DebitAmount = 0m, CreditAmount = 100m }
                ]
            }),
            "error.accounting.partner_required");

        // 4. SalesPostingService scenarios: Cash (CashFund), Bank (BankAccount), Debt (Partner.ReceivableAccountId), and VAT (3331)
        var cashFund = new CashFund { Code = "CF-S26", Name = "S26 Cash Fund", BranchId = subBranch.Id, AccountId = acc1112.Id, IsActive = true };
        var bankAccount = new BankAccount { BankName = "VCB", AccountNumber = "S26-999999", AccountHolder = "ERPm", BranchId = subBranch.Id, AccountId = acc1122.Id, IsActive = true };
        var warehouse = new Warehouse { Code = "WH-S26", Name = "S26 Warehouse", BranchId = subBranch.Id, IsActive = true };
        var partner = new Partner { Code = "KH-S26", Name = "S26 VIP Customer", PartnerType = PartnerType.Customer, ReceivableAccountId = acc1311.Id, CreditLimit = 10000000m, IsActive = true };
        db.CashFunds.Add(cashFund);
        db.BankAccounts.Add(bankAccount);
        db.Warehouses.Add(warehouse);
        db.Partners.Add(partner);
        await db.SaveChangesAsync();

        var uom = await db.UnitsOfMeasure.FirstAsync();
        var item = new Item { Code = "ITEM-S26", Name = "S26 Product", ItemType = ItemType.Goods, BaseUomId = uom.Id, SalePrice = 100000m, IsActive = true };
        db.Items.Add(item);
        await db.SaveChangesAsync();

        db.StockBalances.Add(new StockBalance { WarehouseId = warehouse.Id, ItemId = item.Id, QuantityOnHand = 100m, AverageCostPrice = 60000m, InventoryValue = 6000000m });
        await db.SaveChangesAsync();

        var salesService = new SalesInvoiceService(db, new BranchService(db), new PostingTransaction(db), new TestUser(), null, null, engine);

        // Scenario A: Cash sale via CashFund
        var cashInv = await salesService.CreateInvoiceAsync(new CreateSalesInvoiceRequest
        {
            RequestId = Guid.NewGuid(),
            BranchId = subBranch.Id,
            WarehouseId = warehouse.Id,
            PaymentMethod = PaymentMethod.Cash,
            CashFundId = cashFund.Id,
            PaidAmount = 100000m,
            Items = [new SalesInvoiceItemRequest { ItemId = item.Id, Quantity = 1m, UnitPrice = 100000m }]
        });
        var cashJournal = await db.JournalEntries.Include(j => j.Lines).SingleAsync(j => j.Description == cashInv.InvoiceNumber);
        ctx.Check(
            cashJournal.Lines.Sum(l => l.DebitAmount) == cashJournal.Lines.Sum(l => l.CreditAmount) &&
            cashJournal.Lines.Any(l => l.AccountId == acc1112.Id && l.DebitAmount == 100000m) &&
            cashJournal.Lines.Any(l => l.AccountId == acc5111.Id && l.CreditAmount == 100000m),
            "SalesPostingService posts cash sale using CashFund.AccountId and branch override rule");

        // Scenario B: Bank transfer sale via BankAccount
        var bankInv = await salesService.CreateInvoiceAsync(new CreateSalesInvoiceRequest
        {
            RequestId = Guid.NewGuid(),
            BranchId = subBranch.Id,
            WarehouseId = warehouse.Id,
            PaymentMethod = PaymentMethod.BankTransfer,
            BankAccountId = bankAccount.Id,
            PaidAmount = 100000m,
            Items = [new SalesInvoiceItemRequest { ItemId = item.Id, Quantity = 1m, UnitPrice = 100000m }]
        });
        var bankJournal = await db.JournalEntries.Include(j => j.Lines).SingleAsync(j => j.Description == bankInv.InvoiceNumber);
        ctx.Check(
            bankJournal.Lines.Sum(l => l.DebitAmount) == bankJournal.Lines.Sum(l => l.CreditAmount) &&
            bankJournal.Lines.Any(l => l.AccountId == acc1122.Id && l.DebitAmount == 100000m) &&
            bankJournal.Lines.Any(l => l.AccountId == acc511.Id && l.CreditAmount == 100000m),
            "SalesPostingService posts bank transfer sale using BankAccount.AccountId");

        // Scenario C: Credit (Debt) sale using Partner.ReceivableAccountId + VAT 10% (3331)
        var vatDebtInv = await salesService.CreateInvoiceAsync(new CreateSalesInvoiceRequest
        {
            RequestId = Guid.NewGuid(),
            BranchId = subBranch.Id,
            WarehouseId = warehouse.Id,
            CustomerId = partner.Id,
            PaymentMethod = PaymentMethod.Debt,
            TaxRate = 10m,
            PaidAmount = 0m,
            Items = [new SalesInvoiceItemRequest { ItemId = item.Id, Quantity = 1m, UnitPrice = 100000m }]
        });
        var vatDebtJournal = await db.JournalEntries.Include(j => j.Lines).SingleAsync(j => j.Description == vatDebtInv.InvoiceNumber);
        ctx.Check(
            vatDebtInv.TaxAmount == 10000m && vatDebtInv.TotalAmount == 110000m &&
            vatDebtJournal.Lines.Sum(l => l.DebitAmount) == vatDebtJournal.Lines.Sum(l => l.CreditAmount) &&
            vatDebtJournal.Lines.Any(l => l.AccountId == acc1311.Id && l.DebitAmount == 110000m && l.PartnerId == partner.Id) &&
            vatDebtJournal.Lines.Any(l => l.AccountId == acc511.Id && l.CreditAmount == 100000m) &&
            vatDebtJournal.Lines.Any(l => l.AccountId == acc3331.Id && l.CreditAmount == 10000m),
            "SalesPostingService posts credit sale with VAT (3331) and Partner.ReceivableAccountId");
    }
}
