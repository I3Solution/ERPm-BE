using ERPm.Domain.Entities.Accounting;
using ERPm.Domain.Entities.Catalog;
using ERPm.Domain.Entities.Inventory;
using ERPm.Domain.Entities.Localization;
using ERPm.Domain.Entities.System;
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
        foreach (var (code, name, type) in new[] {
            ("111", "Cash", AccountType.Asset), ("112", "Bank", AccountType.Asset), ("131", "Receivables", AccountType.Asset),
            ("156", "Inventory", AccountType.Asset), ("331", "Payables", AccountType.Liability), ("411", "Opening capital", AccountType.Equity),
            ("511", "Revenue", AccountType.Revenue), ("632", "Cost of sales", AccountType.Expense) })
            if (!await context.Accounts.AnyAsync(a => a.Code == code)) context.Accounts.Add(new Account { Code = code, Name = name, AccountType = type, Translations = new() { ["en"] = name, ["vi"] = name } });
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
        if (!await context.Warehouses.IgnoreQueryFilters().AnyAsync())
        {
            context.Warehouses.Add(new Warehouse { BranchId = root.Id, Code = "MAIN", Name = "Main Warehouse", IsDefault = true, Translations = new() { ["en"] = "Main Warehouse", ["vi"] = "Kho tổng" } });
            await context.SaveChangesAsync();
        }
    }
}
