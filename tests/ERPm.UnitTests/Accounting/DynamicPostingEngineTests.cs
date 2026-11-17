using ERPm.Application.Common;
using ERPm.Domain.Entities.Accounting;
using ERPm.Domain.Entities.System;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Services.Accounting;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace ERPm.UnitTests.Accounting;

public sealed class DynamicPostingEngineTests
{
    private static ApplicationDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task ValidateJournalAsync_ThrowsUnbalancedJournal_WhenDebitDoesNotEqualCredit()
    {
        await using var db = CreateDb(nameof(ValidateJournalAsync_ThrowsUnbalancedJournal_WhenDebitDoesNotEqualCredit));
        db.Accounts.AddRange(
            new Account { Id = 1, Code = "1111", Name = "Tien mat", AccountType = AccountType.Asset, Nature = AccountNature.DebitOnly, IsPostable = true },
            new Account { Id = 2, Code = "131", Name = "Phai thu", AccountType = AccountType.Asset, Nature = AccountNature.Bilateral, IsPostable = true });
        await db.SaveChangesAsync();

        var engine = new DynamicPostingEngine(db);
        var journal = new JournalEntry
        {
            BranchId = 1,
            VoucherNumber = "PT-ERR",
            VoucherType = "RECEIPT",
            Lines =
            [
                new JournalEntryLine { AccountId = 1, DebitAmount = 1000m, CreditAmount = 0m },
                new JournalEntryLine { AccountId = 2, DebitAmount = 0m, CreditAmount = 900m }
            ]
        };

        var ex = await Assert.ThrowsAsync<BusinessException>(() => engine.ValidateJournalAsync(journal));
        Assert.Equal("error.accounting.unbalanced_journal", ex.Message);
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task ResolvePostingRuleAsync_PrefersBranchOverride_AndFallsBackToSystemDefault()
    {
        await using var db = CreateDb(nameof(ResolvePostingRuleAsync_PrefersBranchOverride_AndFallsBackToSystemDefault));
        db.Branches.Add(new Branch { Id = 2, Code = "HN", Name = "Ha Noi", IsActive = true });
        db.Accounts.AddRange(
            new Account { Id = 10, Code = "131", Name = "Phai thu KH", AccountType = AccountType.Asset, Nature = AccountNature.Bilateral, IsPostable = true },
            new Account { Id = 20, Code = "5111", Name = "Doanh thu", AccountType = AccountType.Revenue, Nature = AccountNature.CreditOnly, IsPostable = true },
            new Account { Id = 30, Code = "1311", Name = "Phai thu HN", AccountType = AccountType.Asset, Nature = AccountNature.Bilateral, IsPostable = true });
        db.AccountPostingProfiles.AddRange(
            new AccountPostingProfile
            {
                Id = 1, Module = "SALES", TransactionType = "SALES_REVENUE", BranchId = null, DimensionKey = "DEBT",
                DebitAccountId = 10, CreditAccountId = 20, IsActive = true
            },
            new AccountPostingProfile
            {
                Id = 2, Module = "SALES", TransactionType = "SALES_REVENUE", BranchId = 2, DimensionKey = "DEBT",
                DebitAccountId = 30, CreditAccountId = 20, IsActive = true
            });
        await db.SaveChangesAsync();

        var engine = new DynamicPostingEngine(db);
        var branchRule = await engine.ResolvePostingRuleAsync("SALES", "SALES_REVENUE", 2, "DEBT");
        var defaultRule = await engine.ResolvePostingRuleAsync("SALES", "SALES_REVENUE", 99, "DEBT");

        Assert.Equal(30, branchRule.DebitAccountId);
        Assert.Equal(10, defaultRule.DebitAccountId);
    }
}
