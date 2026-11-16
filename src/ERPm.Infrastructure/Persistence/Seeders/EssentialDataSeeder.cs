using ERPm.Domain.Entities.Accounting;
using ERPm.Domain.Entities.Catalog;
using ERPm.Domain.Entities.Inventory;
using ERPm.Domain.Entities.Localization;
using ERPm.Domain.Entities.System;
using ERPm.Domain.Services.Iam;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace ERPm.Infrastructure.Persistence.Seeders;

public static class EssentialDataSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // Only reference data is provisioned; demo sales, balances and debts are never seeded.
        if (!await context.Branches.IgnoreQueryFilters().AnyAsync(b => b.Code == "root"))
            context.Branches.Add(new Branch { Code = "root", Name = "Head Office", IsDefault = true });
        foreach (var (code, name) in new[] { ("vi", "Tiếng Việt"), ("en", "English") })
            if (!await context.AppLanguages.AnyAsync(l => l.Code == code)) context.AppLanguages.Add(new AppLanguage { Code = code, Name = name, IsDefault = code == "vi" });
        await SeedChartOfAccountsAndPostingProfilesAsync(context);
        if (!await context.UnitsOfMeasure.AnyAsync()) context.UnitsOfMeasure.Add(new UnitOfMeasure { Code = "PCS", Name = "Cái", Translations = new() { ["vi"] = "Cái", ["en"] = "Piece" } });
        await context.SaveChangesAsync();
        using var resource = typeof(EssentialDataSeeder).Assembly.GetManifestResourceStream("ERPm.Infrastructure.Persistence.Seeders.system_dictionary.json")!;
        var dictionary = await JsonSerializer.DeserializeAsync<Dictionary<string, Dictionary<string, string>>>(resource);
        var stored = (await context.AppDictionaries.Select(d => new { d.LanguageCode, d.Key }).ToListAsync()).Select(d => (d.LanguageCode, d.Key)).ToHashSet();
        foreach (var language in dictionary!)
            foreach (var entry in language.Value)
                if (!stored.Contains((language.Key, entry.Key))) context.AppDictionaries.Add(new AppDictionary { LanguageCode = language.Key, Category = "SYSTEM", Key = entry.Key, Value = entry.Value });
        await context.SaveChangesAsync();
        var root = await context.Branches.IgnoreQueryFilters().SingleAsync(b => b.Code == "root");
        if (root.OrgUnitType != OrgUnitType.Group && root.ParentId == null)
        {
            root.OrgUnitType = OrgUnitType.Group;
            await context.SaveChangesAsync();
        }
        await SeedRolesAsync(context);
        await NotificationTemplateSeeder.SeedAsync(context);
        if (!await context.Warehouses.IgnoreQueryFilters().AnyAsync())
        {
            context.Warehouses.Add(new Warehouse { BranchId = root.Id, Code = "MAIN", Name = "Main Warehouse", IsDefault = true, Translations = new() { ["en"] = "Main Warehouse", ["vi"] = "Kho tổng" } });
            await context.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Legacy system roles are re-synchronised from code on every run; group templates are only created when
    /// missing so super administrators may adjust them.
    /// </summary>
    private static async Task SeedRolesAsync(ApplicationDbContext context)
    {
        var roles = await context.IamRoles.IgnoreQueryFilters().Where(r => r.CompanyId == null).ToListAsync();
        foreach (var (code, name, policy) in SystemRoles.Legacy)
        {
            var role = roles.FirstOrDefault(r => r.Code == code);
            if (role == null) context.IamRoles.Add(role = new IamRole { Code = code });
            role.Name = name;
            role.IsSystemRole = true;
            role.IsActive = true;
            role.IsDeleted = false;
            role.PolicyDocument = policy;
        }
        foreach (var (code, name, policy) in SystemRoles.Templates)
            if (roles.All(r => r.Code != code)) context.IamRoles.Add(new IamRole { Code = code, Name = name, PolicyDocument = policy });
        await context.SaveChangesAsync();
    }

    private static async Task SeedChartOfAccountsAndPostingProfilesAsync(ApplicationDbContext context)
    {
        var specs = new (string Code, string ViName, string EnName, AccountType Type, AccountNature Nature, bool IsPostable, bool TrackPartner, bool TrackBranch, string? ParentCode)[]
        {
            ("111", "Tiền mặt", "Cash on Hand", AccountType.Asset, AccountNature.DebitOnly, false, false, true, null),
            ("1111", "Tiền Việt Nam", "Cash in VND", AccountType.Asset, AccountNature.DebitOnly, true, false, true, "111"),
            ("112", "Tiền gửi ngân hàng", "Cash in Bank", AccountType.Asset, AccountNature.DebitOnly, false, false, true, null),
            ("1121", "Tiền Việt Nam tại ngân hàng", "Bank Deposits in VND", AccountType.Asset, AccountNature.DebitOnly, true, false, true, "112"),
            ("131", "Phải thu của khách hàng", "Accounts Receivable", AccountType.Asset, AccountNature.Bilateral, true, true, true, null),
            ("141", "Tạm ứng", "Employee Advances", AccountType.Asset, AccountNature.DebitOnly, true, true, true, null),
            ("156", "Hàng hóa", "Merchandise Inventory", AccountType.Asset, AccountNature.DebitOnly, true, false, true, null),
            ("331", "Phải trả cho người bán", "Accounts Payable", AccountType.Liability, AccountNature.Bilateral, true, true, true, null),
            ("3331", "Thuế GTGT phải nộp", "VAT Payable", AccountType.Liability, AccountNature.CreditOnly, true, false, true, null),
            ("334", "Phải trả người lao động", "Payable to Employees", AccountType.Liability, AccountNature.CreditOnly, true, false, true, null),
            ("411", "Vốn đầu tư của chủ sở hữu", "Owner's Equity", AccountType.Equity, AccountNature.CreditOnly, true, false, true, null),
            ("511", "Doanh thu bán hàng và cung cấp dịch vụ", "Sales Revenue", AccountType.Revenue, AccountNature.CreditOnly, true, false, true, null),
            ("521", "Các khoản giảm trừ doanh thu", "Sales Deductions", AccountType.Revenue, AccountNature.DebitOnly, true, false, true, null),
            ("632", "Giá vốn hàng bán", "Cost of Goods Sold", AccountType.Expense, AccountNature.DebitOnly, true, false, true, null),
            ("642", "Chi phí quản lý doanh nghiệp", "General & Administrative Expenses", AccountType.Expense, AccountNature.DebitOnly, true, false, true, null),
            ("711", "Thu nhập khác", "Other Income", AccountType.Revenue, AccountNature.CreditOnly, true, false, true, null),
            ("811", "Chi phí khác", "Other Expenses", AccountType.Expense, AccountNature.DebitOnly, true, false, true, null),
            ("911", "Xác định kết quả kinh doanh", "Income Summary", AccountType.Equity, AccountNature.Bilateral, true, false, true, null)
        };

        var existing = await context.Accounts.IgnoreQueryFilters().ToDictionaryAsync(a => a.Code);
        foreach (var s in specs)
        {
            if (!existing.TryGetValue(s.Code, out var account))
            {
                account = new Account
                {
                    Code = s.Code,
                    Name = s.ViName,
                    AccountType = s.Type,
                    Nature = s.Nature,
                    IsPostable = s.IsPostable,
                    TrackPartner = s.TrackPartner,
                    TrackBranch = s.TrackBranch,
                    Translations = new() { ["vi"] = s.ViName, ["en"] = s.EnName }
                };
                context.Accounts.Add(account);
                existing[s.Code] = account;
            }
            else
            {
                account.Nature = s.Nature;
                account.IsPostable = s.IsPostable;
                account.TrackPartner = s.TrackPartner;
                account.TrackBranch = s.TrackBranch;
            }
        }
        await context.SaveChangesAsync();

        var parentChanged = false;
        foreach (var s in specs.Where(x => x.ParentCode != null))
        {
            var child = existing[s.Code];
            var parent = existing[s.ParentCode!];
            if (child.ParentId != parent.Id)
            {
                child.ParentId = parent.Id;
                parentChanged = true;
            }
        }
        if (parentChanged) await context.SaveChangesAsync();

        var profileSpecs = new (string Module, string TransactionType, string? DimensionKey, string DebitCode, string CreditCode, string Description)[]
        {
            ("SD", "SALES_REVENUE", "CASH", "1111", "511", "Cash sales revenue"),
            ("SD", "SALES_REVENUE", "BANK", "1121", "511", "Bank transfer sales revenue"),
            ("SD", "SALES_REVENUE", "DEBT", "131", "511", "Credit sales revenue (AR)"),
            ("SD", "SALES_VAT", null, "131", "3331", "Output VAT on credit sales"),
            ("SD", "SALES_COGS", null, "632", "156", "Cost of goods sold on delivery"),
            ("SD", "SALES_RETURN", null, "521", "131", "Sales return deduction"),
            ("ACC", "RECEIPT_CUSTOMER", "CASH", "1111", "131", "Customer debt collection via cash"),
            ("ACC", "RECEIPT_CUSTOMER", "BANK", "1121", "131", "Customer debt collection via bank"),
            ("ACC", "RECEIPT_ADVANCE", "CASH", "1111", "131", "Customer advance receipt via cash"),
            ("ACC", "RECEIPT_ADVANCE", "BANK", "1121", "131", "Customer advance receipt via bank"),
            ("ACC", "RECEIPT_OTHER", "CASH", "1111", "711", "Other receipt via cash"),
            ("ACC", "RECEIPT_OTHER", "BANK", "1121", "711", "Other receipt via bank"),
            ("ACC", "PAYMENT_SUPPLIER", "CASH", "331", "1111", "Supplier payment via cash"),
            ("ACC", "PAYMENT_SUPPLIER", "BANK", "331", "1121", "Supplier payment via bank"),
            ("ACC", "PAYMENT_EXPENSE", "CASH", "642", "1111", "Operating expense payment via cash"),
            ("ACC", "PAYMENT_EXPENSE", "BANK", "642", "1121", "Operating expense payment via bank"),
            ("ACC", "PAYMENT_SALARY", "CASH", "334", "1111", "Salary payment via cash"),
            ("ACC", "PAYMENT_SALARY", "BANK", "334", "1121", "Salary payment via bank"),
            ("ACC", "PAYMENT_ADVANCE", "CASH", "141", "1111", "Employee advance payment via cash"),
            ("ACC", "PAYMENT_ADVANCE", "BANK", "141", "1121", "Employee advance payment via bank"),
            ("ACC", "PAYMENT_OTHER", "CASH", "811", "1111", "Other payment via cash"),
            ("ACC", "PAYMENT_OTHER", "BANK", "811", "1121", "Other payment via bank"),
            ("ACC", "INTERNAL_TRANSFER", "BANK_TO_CASH", "1111", "1121", "Bank withdrawal to cash fund"),
            ("ACC", "INTERNAL_TRANSFER", "CASH_TO_BANK", "1121", "1111", "Cash deposit to bank account")
        };

        var existingProfiles = await context.AccountPostingProfiles
            .IgnoreQueryFilters()
            .Where(p => !p.IsDeleted && p.BranchId == null)
            .Select(p => new { p.Module, p.TransactionType, p.DimensionKey })
            .ToListAsync();
        var profileKeys = existingProfiles.Select(p => (p.Module, p.TransactionType, p.DimensionKey)).ToHashSet();

        foreach (var p in profileSpecs)
        {
            if (profileKeys.Contains((p.Module, p.TransactionType, p.DimensionKey))) continue;
            context.AccountPostingProfiles.Add(new AccountPostingProfile
            {
                Module = p.Module,
                TransactionType = p.TransactionType,
                BranchId = null,
                DimensionKey = p.DimensionKey,
                DebitAccountId = existing[p.DebitCode].Id,
                CreditAccountId = existing[p.CreditCode].Id,
                Description = p.Description,
                IsActive = true
            });
        }
        await context.SaveChangesAsync();
    }
}
