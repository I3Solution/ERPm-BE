using System.Text.Json;
using ERPm.Application.Common;
using ERPm.Domain.Entities.HumanResource;
using ERPm.Domain.Entities.Inventory;
using ERPm.Application.Common.Crud;
using ERPm.Domain.Services.Iam;
using ERPm.Domain.Services.Sales;
using ERPm.Application.Common.Interfaces;
using ERPm.Application.DTOs.Inventory;
using ERPm.Application.DTOs.Sales;
using ERPm.Application.DTOs.Partner;
using ERPm.Domain.Entities.Accounting;
using ERPm.Domain.Entities.Catalog;
using ERPm.Domain.Entities.Partner;
using ERPm.Domain.Entities.Sales;
using ERPm.Domain.Entities.System;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Persistence.Seeders;
using ERPm.Infrastructure.Services;
using ERPm.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;

var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var connection = Environment.GetEnvironmentVariable("ERPM_TEST_CONNECTION");
if (string.IsNullOrEmpty(connection))
{
    using var settings = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, "src/ERPm.API/appsettings.local.json")));
    connection = settings.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString();
}
var source = new NpgsqlConnectionStringBuilder(connection) { Database = "postgres", IncludeErrorDetail = true };
var setup = args.Contains("--setup-local");
var testName = (setup ? "erpm_mvp_local_" : "erpm_verify_") + Guid.NewGuid().ToString("N");
await using var admin = new NpgsqlConnection(source.ConnectionString);
await admin.OpenAsync();
await using (var command = new NpgsqlCommand($"CREATE DATABASE {testName}", admin)) await command.ExecuteNonQueryAsync();
Console.WriteLine($"Isolated verification database: {testName}");
source.Database = testName;
ApplicationDbContext Db(ICurrentUser? user = null) => new(new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseNpgsql(source.ConnectionString, o => o.EnableRetryOnFailure()).UseSnakeCaseNamingConvention().Options, user);
var user = new TestUser();
if (setup)
{
    await using var local = Db(user);
    await local.Database.MigrateAsync(); await EssentialDataSeeder.SeedAsync(local); await OrganizationSampleSeeder.SeedAsync(local);
    var password = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
    var account = new AppUser { UserName = "ADMIN", DisplayName = "Administrator", Role = "Admin" };
    account.PasswordHash = new Microsoft.AspNetCore.Identity.PasswordHasher<AppUser>().HashPassword(account, password);
    local.AppUsers.Add(account); await local.SaveChangesAsync();
    source.IncludeErrorDetail = false;
    await File.WriteAllTextAsync(Path.Combine(repo, "src/ERPm.API/appsettings.local.json"), JsonSerializer.Serialize(new { ConnectionStrings = new { DefaultConnection = source.ConnectionString }, Database = new { AutoMigrate = false } }, new JsonSerializerOptions { WriteIndented = true }));
    var credentials = Path.GetFullPath(Path.Combine(repo, "../.ai/erpm-local.credentials.local.json"));
    await File.WriteAllTextAsync(credentials, JsonSerializer.Serialize(new { UserName = "admin", Password = password, Database = testName, ApiUrl = "http://localhost:5000", FrontendUrl = "http://localhost:4200" }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine("Local database and administrator initialized. Credentials: " + credentials);
    return;
}
var checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAILED: " + name); checks++; Console.WriteLine("PASS: " + name); }
async Task Reject(Func<Task> action, string code)
{
    try { await action(); throw new Exception("Expected rejection: " + code); }
    catch (BusinessException e) { Check(e.Code == code, code); }
}
try
{
    await using var db = Db(user);
    await db.GetService<IMigrator>().MigrateAsync("20261011071959_Initial_Core_Schema");
    var legacyUnit = Guid.NewGuid(); var legacyItem = Guid.NewGuid();
    await db.Database.ExecuteSqlInterpolatedAsync($$"""
        INSERT INTO units_of_measure ("Id","Code","Name","Translations","IsActive","CreatedAtUtc","IsDeleted")
        VALUES ({{legacyUnit}},'LEGACY_UNIT','Legacy unit','{}'::jsonb,true,NOW(),false);
        INSERT INTO items ("Id","Code","Name","Translations","ItemType","BaseUomId","SalePrice","CostPrice","MinStockAlert","MaxStockAlert","IsActive","CreatedAtUtc","IsDeleted")
        VALUES ({{legacyItem}},'LEGACY_ITEM','Legacy item','{}'::jsonb,1,{{legacyUnit}},1,1,0,0,true,NOW(),false);
        """);
    await db.Database.MigrateAsync();
    await EssentialDataSeeder.SeedAsync(db);
    Check(await db.Items.AnyAsync(i => i.ExternalId == legacyItem && i.BaseUom!.ExternalId == legacyUnit), "legacy UUID rows and foreign keys survive upgrade");
    Check(!await db.Database.HasPendingModelChangesAsyncCompat(), "migration matches current model");
    var branch = await db.Branches.SingleAsync();
    Check(branch.ExternalId.Version == 7, "new root branch uses a valid UUIDv7 external key");
    var warehouse = await db.Warehouses.SingleAsync();
    var uom = await db.UnitsOfMeasure.FirstAsync();
    var customer = new Partner { Code = "CUSTOMER", Name = "Customer", CreditLimit = 100000, PartnerType = PartnerType.Customer };
    var supplier = new Partner { Code = "SUPPLIER", Name = "Supplier", PartnerType = PartnerType.Supplier };
    var item = new Item { Code = "ITEM", Name = "Item", BaseUomId = uom.Id, SalePrice = 100, CostPrice = 1 };
    db.Partners.AddRange(customer, supplier); db.Items.Add(item); await db.SaveChangesAsync();
    StockReceiptPostingService Receipts(ApplicationDbContext c) => new(c, new PostingTransaction(c), user);
    SalesInvoiceService Sales(ApplicationDbContext c) => new(c, new BranchService(c), new PostingTransaction(c));
    CreateStockReceiptRequest Receipt(decimal quantity, decimal price) => new() { RequestId = Guid.NewGuid(), WarehouseId = warehouse.Id, SupplierId = supplier.Id, Items = [new() { ItemId = item.Id, Quantity = quantity, UnitPrice = price }] };
    CreateSalesInvoiceRequest Sale(decimal quantity = 1, decimal paid = 100) => new() { RequestId = Guid.NewGuid(), WarehouseId = warehouse.Id, CustomerId = customer.Id, PaidAmount = paid, Items = [new() { ItemId = item.Id, Quantity = quantity, UnitPrice = 100 }] };
    // Delivery addresses: many per partner, a single live default, the flag defaults to limited credit.
    Check(!customer.IsUnlimitedCredit, "a new partner has a limited credit by default");
    db.PartnerDeliveryAddresses.AddRange(
        new PartnerDeliveryAddress { PartnerId = customer.Id, RecipientName = "Warehouse A", Address = "1 Main St", IsDefault = true },
        new PartnerDeliveryAddress { PartnerId = customer.Id, RecipientName = "Warehouse B", Address = "2 Side St" });
    await db.SaveChangesAsync();
    Check(await db.PartnerDeliveryAddresses.CountAsync(a => a.PartnerId == customer.Id) == 2, "a partner can keep several delivery addresses");
    var secondDefault = false;
    try
    {
        await using var other = Db(user);
        other.PartnerDeliveryAddresses.Add(new PartnerDeliveryAddress { PartnerId = customer.Id, RecipientName = "Warehouse C", Address = "3 Other St", IsDefault = true });
        await other.SaveChangesAsync();
    }
    catch (DbUpdateException) { secondDefault = true; }
    Check(secondDefault, "a partner cannot have two live default delivery addresses");
    var dashboard = new DashboardService(db, new BranchService(db));
    var empty =await dashboard.GetMetricsAsync();
    Check(empty.TodaySales == 0 && empty.TodayInvoiceCount == 0 && empty.TotalInventoryValue == 0, "empty dashboard returns zero");
    await Receipts(db).CreateAsync(Receipt(10, 10), default);
    await Receipts(db).CreateAsync(Receipt(10, 30), default);
    db.ChangeTracker.Clear();
    Check((await db.StockBalances.SingleAsync()).AverageCostPrice == 20, "weighted average receipt cost");
    var request = Sale(paid: 40);
    var result = await Sales(db).CreateInvoiceAsync(request);
    Check(result.PaidAmount == 40 && result.RemainingDebt == 60, "partial payment amounts");
    var stored = await db.SalesInvoices.Include(i => i.JournalEntry!).ThenInclude(j => j.Lines).Include(i => i.Items).SingleAsync(i => i.Id == result.Id);
    var accounts = await db.Accounts.ToDictionaryAsync(a => a.Id, a => a.Code);
    Check(stored.Items.Single().CostPrice == 20, "sale uses warehouse average cost, not catalogue cost");
    Check(stored.JournalEntry!.Lines.Where(l => accounts[l.AccountId] == "111").Sum(l => l.DebitAmount) == 40 && stored.JournalEntry.Lines.Where(l => accounts[l.AccountId] == "131").Sum(l => l.DebitAmount) == 60, "journal splits cash and receivable");
    var replay = await Sales(db).CreateInvoiceAsync(request);
    Check(replay.Id == result.Id && await db.SalesInvoices.CountAsync() == 1, "retry returns same invoice");
    request.PaidAmount = 50;
    await Reject(async () => await Sales(db).CreateInvoiceAsync(request), "error.request.conflict");
    var cash = await Sales(db).CreateInvoiceAsync(Sale(paid: 200));
    Check(cash.PaidAmount == 100 && cash.ChangeAmount == 100 && cash.TenderedAmount == 200, "cash tender and change separated");
    var zero = await Sales(db).CreateInvoiceAsync(Sale(paid: 0));
    Check(zero.PaidAmount == 0 && zero.RemainingDebt == 100, "zero payment is not silently paid in full");
    var negative = Sale(); negative.Items[0].Quantity = -1;
    await Reject(async () => await Sales(db).CreateInvoiceAsync(negative), "error.validation.number");
    await Reject(async () => await Sales(db).CreateInvoiceAsync(Sale(10000)), "error.stock.insufficient");
    var duplicate = Receipt(1, 20); duplicate.Items.Add(new() { ItemId = item.Id, Quantity = 2, UnitPrice = 20 });
    await Receipts(db).CreateAsync(duplicate, default);
    Check(await db.StockBalances.CountAsync() == 1, "duplicate receipt lines share one balance");
    db.ChangeTracker.Clear();
    var before = (await db.StockBalances.SingleAsync()).QuantityOnHand;
    await Task.WhenAll(Enumerable.Range(0, 8).Select(async index =>
    {
        await using var concurrent = Db(user);
        if (index % 2 == 0) await Sales(concurrent).CreateInvoiceAsync(Sale());
        else await Receipts(concurrent).CreateAsync(Receipt(1, 20), default);
    }));
    db.ChangeTracker.Clear();
    Check((await db.StockBalances.SingleAsync()).QuantityOnHand == before, "concurrent sales and receipts do not lose updates");
    var beforeFailure = (await db.StockBalances.SingleAsync()).QuantityOnHand;
    var revenue = await db.Accounts.SingleAsync(a => a.Code == "511"); revenue.IsActive = false; await db.SaveChangesAsync();
    await Reject(async () => await Sales(db).CreateInvoiceAsync(Sale()), "error.accounting.accounts_missing");
    db.ChangeTracker.Clear();
    Check((await db.StockBalances.SingleAsync()).QuantityOnHand == beforeFailure, "failed posting rolls back stock");
    var tracked = await db.Items.SingleAsync(i => i.Id == item.Id); tracked.Attributes = new() { ["color"] = "red" }; await db.SaveChangesAsync();
    tracked.Attributes = new() { ["color"] = "blue" }; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    Check((await db.Items.SingleAsync(i => i.Id == item.Id)).Attributes["color"].ToString() == "blue", "JSONB same-key update persists");
    var cancelTarget = await db.SalesInvoices.FirstAsync(i => i.Id == result.Id);
    var beforeCancel = (await db.StockBalances.SingleAsync()).QuantityOnHand;
    await Sales(db).CancelInvoiceAsync(cancelTarget.Id, "Verification reversal");
    db.ChangeTracker.Clear();
    Check((await db.StockBalances.SingleAsync()).QuantityOnHand == beforeCancel + 1, "cancellation restores stock");
    Check(await db.JournalEntries.CountAsync(j => j.ReversesJournalEntryId == cancelTarget.JournalEntryId) == 1, "cancellation creates one reversing journal");
    await Sales(db).CancelInvoiceAsync(cancelTarget.Id, "Verification reversal");
    Check(await db.JournalEntries.CountAsync(j => j.ReversesJournalEntryId == cancelTarget.JournalEntryId) == 1, "repeated cancellation is idempotent");
    // Price lists: unique live code, one price per item/unit/tier (NULL unit included), value ranges, partner link, soft delete.
    async Task<bool> Rejected(Func<Task> action) { try { await action(); return false; } catch (DbUpdateException) { db.ChangeTracker.Clear(); return true; } }
    var priceList = new PriceList
    {
        Code = "PL-RETAIL",
        Name = "Retail",
        Versions =
        [
            new PriceListVersion
            {
                VersionCode = "V1",
                Status = PriceListVersionStatus.Active,
                EffectiveFrom = DateTime.UtcNow.AddDays(-1),
                Items =
                [
                    new() { ItemId = item.Id, UnitPrice = 90, MinQuantity = 0 },
                    new() { ItemId = item.Id, UnitPrice = 80, MinQuantity = 10, DiscountPercent = 5 }
                ]
            }
        ]
    };
    db.PriceLists.Add(priceList); await db.SaveChangesAsync();
    var versionId = priceList.Versions.Single().Id;
    Check((await db.PriceLists.Include(p => p.Versions).ThenInclude(v => v.Items).SingleAsync(p => p.Code == "PL-RETAIL")).Versions.Single().Items.Count == 2 && priceList.ExternalId.Version == 7, "price list keeps its quantity tiers in V1");
    Check(await Rejected(async () => { db.PriceLists.Add(new PriceList { Code = "PL-RETAIL", Name = "Duplicate" }); await db.SaveChangesAsync(); }), "price list code is unique");
    Check(await Rejected(async () => { db.PriceListItems.Add(new PriceListItem { PriceListVersionId = versionId, ItemId = item.Id, UnitPrice = 70, MinQuantity = 10 }); await db.SaveChangesAsync(); }), "same item, unit and tier cannot be priced twice in a version");
    Check(await Rejected(async () => { db.PriceListItems.Add(new PriceListItem { PriceListVersionId = versionId, ItemId = item.Id, UnitPrice = -1, MinQuantity = 20 }); await db.SaveChangesAsync(); }), "negative price is rejected");
    Check(await Rejected(async () => { db.PriceListItems.Add(new PriceListItem { PriceListVersionId = versionId, ItemId = item.Id, UnitPrice = 1, MinQuantity = 20, DiscountPercent = 101 }); await db.SaveChangesAsync(); }), "discount above 100 percent is rejected");
    Check(await Rejected(async () => { db.PriceListVersions.Add(new PriceListVersion { PriceListId = priceList.Id, VersionCode = "V1", EffectiveFrom = DateTime.UtcNow, Status = PriceListVersionStatus.Draft }); await db.SaveChangesAsync(); }), "version code is unique within a price list");
    var linked = await db.Partners.SingleAsync(p => p.Id == customer.Id); linked.PriceListId = priceList.Id; await db.SaveChangesAsync();
    Check((await db.Partners.SingleAsync(p => p.Id == customer.Id)).PriceListId == priceList.Id, "customer keeps its default price list");
    linked.PriceListId = null; (await db.PriceLists.SingleAsync(p => p.Id == priceList.Id)).IsDeleted = true; await db.SaveChangesAsync(); db.ChangeTracker.Clear();
    Check(!await db.PriceLists.AnyAsync() && !await db.PriceListItems.AnyAsync(), "deleted price list and its lines are hidden");
    db.PriceLists.Add(new PriceList { Code = "PL-RETAIL", Name = "Reused code" }); await db.SaveChangesAsync();
    Check(await db.PriceLists.CountAsync() == 1, "deleted price list code can be reused");
    // Pricing engine (pure rules) and price list service.
    PriceTier T(decimal min, decimal price, decimal pct = 0) => new(min, price, pct);
    PriceCandidate L(int id, bool own, bool branchOnly, params PriceTier[] tiers) => new(id, "L" + id, own, branchOnly, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), tiers);
    var tiered = PricingEngine.Price(100, 12, [L(1, false, false, T(0, 90), T(10, 80, 5))]);
    Check(tiered.UnitPrice == 80 && tiered.DiscountAmount == 48 && tiered.LineTotal == 912 && tiered.AppliedPriceListId == 1 && tiered.OriginalPrice == 100, "pricing picks the highest quantity tier and its discount");
    Check(PricingEngine.Price(100, 5, [L(1, false, false, T(10, 80))]).UnitPrice == 100 && PricingEngine.Price(100, 5, []).AppliedPriceListId == null, "pricing falls back to the catalogue price");
    Check(PricingEngine.Price(100, 1, [L(1, false, false, T(0, 70)), L(2, false, false, T(0, 80, 20))]).AppliedPriceListId == 2, "pricing without own list picks the lowest net price");
    Check(PricingEngine.Price(100, 1, [L(1, false, false, T(0, 60)), L(2, true, false, T(0, 90))]).AppliedPriceListId == 2, "the customer's own list wins over a cheaper one");
    Check(PricingEngine.Price(100, 1, [L(1, false, false, T(0, 70)), L(2, false, true, T(0, 70))]).AppliedPriceListId == 2, "equal prices prefer the branch-specific list");
    var inclusive = PricingEngine.Price(100, 1, [new PriceCandidate(3, "Inclusive", false, false, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), [T(0, 110)], IsTaxInclusive: true)], 10);
    Check(inclusive.UnitPrice == 100 && inclusive.TaxAmount == 10 && inclusive.TotalWithTax == 110 && inclusive.IsTaxInclusive, "tax-inclusive prices are decomposed into net and VAT");
    var exclusive = PricingEngine.Price(100, 1, [L(4, false, false, T(0, 100))], 10);
    Check(exclusive.UnitPrice == 100 && exclusive.TaxAmount == 10 && exclusive.TotalWithTax == 110 && !exclusive.IsTaxInclusive, "tax-exclusive prices add VAT to the net amount");
    Check(PermissionCatalog.Contains("sd:pricelist:view") && PermissionCatalog.Contains("sd:pricelist:edit") && PermissionCatalog.Contains("sd:pricelist:delete"), "price list permissions are in the catalog");

    var lists = new PriceListService(db);
    var from = DateTime.UtcNow.AddDays(-1);
    CreatePriceListRequest ListRequest(string code, decimal price, int? branchId = null, bool active = true, DateTime? to = null) =>
        new() { Code = code, Name = code, Currency = "vnd", EffectiveFrom = from, EffectiveTo = to, BranchId = branchId, IsActive = active, Items = [new() { ItemId = item.Id, UnitPrice = price }] };
    var wholesale = await lists.CreateAsync(ListRequest("wholesale", 70));
    Check(wholesale.Code == "WHOLESALE" && wholesale.Currency == "VND" && wholesale.Items.Single().ItemCode == "ITEM" && wholesale.Items.Single().UnitName != null, "price list is created normalized and returns its lines");
    await Reject(async () => await lists.CreateAsync(ListRequest("Wholesale", 1)), "error.sd.pricelist_duplicate_code");
    var bad = ListRequest("BAD", 1); bad.Items[0].DiscountPercent = 101;
    await Reject(async () => await lists.CreateAsync(bad), "error.validation.number");
    var twice = ListRequest("TWICE", 1); twice.Items.Add(new() { ItemId = item.Id, UnitPrice = 2 });
    await Reject(async () => await lists.CreateAsync(twice), "error.validation.request");
    var backwards = ListRequest("BACK", 1, to: from.AddDays(-1));
    await Reject(async () => await lists.CreateAsync(backwards), "error.validation.request");
    var vip = await lists.CreateAsync(ListRequest("VIP", 60, branch.Id));
    await lists.CreateAsync(ListRequest("OFF", 10, active: false));
    await lists.CreateAsync(ListRequest("OLD", 5, to: from.AddHours(1)));
    var paged = await lists.GetPagedAsync(new BaseFilterQuery { Keyword = "vip" });
    Check(paged.TotalCount == 1 && paged.Items.Single().BranchName == branch.Name && paged.Items.Single().Items.Count == 0, "price list page returns headers without lines");

    var pricing = (IPricingService)lists;
    CalculatePriceRequest Quote(int? customerId, decimal quantity = 1) => new() { CustomerId = customerId, BranchId = branch.Id, Items = [new() { ItemId = item.Id, Quantity = quantity }] };
    var anonymous = (await pricing.CalculateAsync(Quote(null))).Items.Single();
    Check(anonymous.AppliedPriceListId == vip.Id && anonymous.UnitPrice == 60 && anonymous.OriginalPrice == 100 && anonymous.LineTotal == 60, "calculate-price picks the best list and ignores inactive and expired ones");
    await db.Partners.Where(p => p.Id == customer.Id).ExecuteUpdateAsync(p => p.SetProperty(c => c.PriceListId, wholesale.Id));
    var own = (await pricing.CalculateAsync(Quote(customer.Id, 3))).Items.Single();
    Check(own.AppliedPriceListId == wholesale.Id && own.UnitPrice == 70 && own.LineTotal == 210, "calculate-price uses the customer's own price list");
    await Reject(async () => await pricing.CalculateAsync(Quote(supplier.Id)), "error.sales.customer");
    await Reject(async () => await pricing.CalculateAsync(Quote(null, 0)), "error.validation.number");

    var edit = ListRequest("WHOLESALE", 65); edit.Items.Add(new() { ItemId = item.Id, UnitPrice = 55, MinQuantity = 10 });
    var keptLine = wholesale.Items.Single().Id;
    var edited = await lists.UpdateAsync(wholesale.Id, new UpdatePriceListRequest { Code = edit.Code, Name = "Wholesale 2", Currency = "VND", EffectiveFrom = from, IsActive = true, Items = edit.Items });
    Check(edited!.Name == "Wholesale 2" && edited.Items.Count == 2 && edited.Items.Any(i => i.Id == keptLine && i.UnitPrice == 65), "updating a price list keeps unchanged lines and adds new tiers");
    var shrunk = await lists.UpdateAsync(wholesale.Id, new UpdatePriceListRequest { Code = "WHOLESALE", Name = "Wholesale 2", Currency = "VND", EffectiveFrom = from, IsActive = true, Items = [new() { ItemId = item.Id, UnitPrice = 65 }] });
    Check(shrunk!.Items.Count == 1, "updating a price list removes dropped tiers");
    var sourceVersion = await db.PriceListVersions.SingleAsync(v => v.PriceListId == wholesale.Id);
    var clonedVersion = await lists.CloneVersionAsync(wholesale.Id, new ClonePriceListVersionRequest
    {
        SourceVersionId = sourceVersion.Id,
        VersionCode = "V2",
        EffectiveFrom = from.AddDays(5)
    });
    Check(clonedVersion.Status == PriceListVersionStatus.Draft && clonedVersion.ItemCount == 1 && clonedVersion.Items.Single().UnitPrice == 65, "cloning a price list version copies its price tiers into a Draft version");
    var adjusted = await lists.BulkAdjustVersionAsync(wholesale.Id, clonedVersion.Id, new BulkPriceAdjustmentRequest
    {
        AdjustmentType = PriceAdjustmentType.Percentage,
        Value = 10,
        RoundingIncrement = 0.01m
    });
    Check(adjusted.AdjustedLineCount == 1 && (await db.PriceListVersions.Include(v => v.Items).SingleAsync(v => v.Id == clonedVersion.Id)).Items.Single().UnitPrice == 71.5m, "bulk percentage adjustment updates and rounds all Draft version prices");
    var fixedAdjusted = await lists.BulkAdjustVersionAsync(wholesale.Id, clonedVersion.Id, new BulkPriceAdjustmentRequest
    {
        AdjustmentType = PriceAdjustmentType.FixedAmount,
        Value = 0.25m,
        RoundingIncrement = 0.5m
    });
    Check(fixedAdjusted.AdjustedLineCount == 1 && (await db.PriceListVersions.Include(v => v.Items).SingleAsync(v => v.Id == clonedVersion.Id)).Items.Single().UnitPrice == 72m, "bulk fixed-amount adjustment rounds to the configured increment");
    var listedVersions = await lists.GetVersionsAsync(wholesale.Id);
    Check(listedVersions.Count == 2 && listedVersions[0].Id == clonedVersion.Id && listedVersions[0].VersionCode == "V2" && listedVersions[0].ItemCount == 1 && listedVersions[0].Items.Count == 0
        && listedVersions[1].Id == sourceVersion.Id && listedVersions[1].VersionCode == "V1", "listing price list versions returns lightweight version headers without loading items");
    var fetchedDraftVersion = await lists.GetVersionAsync(wholesale.Id, clonedVersion.Id);
    Check(fetchedDraftVersion.Id == clonedVersion.Id && fetchedDraftVersion.Status == PriceListVersionStatus.Draft && fetchedDraftVersion.Items.Count == 1
        && fetchedDraftVersion.Items.Single().UnitPrice == 72m && fetchedDraftVersion.Items.Single().ItemCode == "ITEM", "reading a specific price list version returns its items after cloning and adjustment");
    await Reject(async () => await lists.GetVersionsAsync(999999), "error.validation.reference");
    await Reject(async () => await lists.GetVersionAsync(wholesale.Id, 999999), "error.validation.reference");
    await db.PriceListVersions.Where(v => v.Id == sourceVersion.Id).ExecuteUpdateAsync(v => v.SetProperty(x => x.EffectiveTo, from.AddDays(4)));
    await db.PriceListVersions.Where(v => v.Id == clonedVersion.Id).ExecuteUpdateAsync(v => v.SetProperty(x => x.Status, PriceListVersionStatus.Active));
    var beforeVersion = (await lists.CalculateAsync(new CalculatePriceRequest { CustomerId = customer.Id, BranchId = branch.Id, DocumentDate = from.AddDays(2), Items = [new() { ItemId = item.Id, Quantity = 1 }] })).Items.Single();
    var afterVersion = (await lists.CalculateAsync(new CalculatePriceRequest { CustomerId = customer.Id, BranchId = branch.Id, DocumentDate = from.AddDays(6), Items = [new() { ItemId = item.Id, Quantity = 1 }] })).Items.Single();
    Check(beforeVersion.AppliedVersionCode == "V1" && afterVersion.AppliedVersionCode == "V2" && afterVersion.UnitPrice == 72m, "pricing selects the Active version effective on the document date");
    await Reject(async () => await lists.BulkAdjustVersionAsync(wholesale.Id, sourceVersion.Id, new BulkPriceAdjustmentRequest { AdjustmentType = PriceAdjustmentType.FixedAmount, Value = 1 }), "error.sd.pricelist_version_not_draft");

    // S25.2b: type filter, version lifecycle (Draft -> Active -> Expired) and Draft item CRUD.
    var vendorList = await lists.CreateAsync(new CreatePriceListRequest { Code = "VENDOR-BASE", Name = "Vendor Base", Currency = "VND", Type = PriceListType.Purchase, EffectiveFrom = from, IsActive = true });
    var salesFiltered = await lists.GetPagedAsync(new PriceListFilterQuery { Type = PriceListType.Sales, PageSize = 50 });
    var purchaseFiltered = await lists.GetPagedAsync(new PriceListFilterQuery { Type = PriceListType.Purchase, PageSize = 50 });
    var unfilteredLists = await lists.GetPagedAsync(new PriceListFilterQuery { PageSize = 50 });
    Check(salesFiltered.Items.All(l => l.Type == PriceListType.Sales) && salesFiltered.Items.Any(l => l.Id == wholesale.Id) && salesFiltered.Items.All(l => l.Id != vendorList.Id), "filtering price lists by Sales returns only sales price lists");
    Check(purchaseFiltered.Items.All(l => l.Type == PriceListType.Purchase) && purchaseFiltered.Items.Any(l => l.Id == vendorList.Id) && purchaseFiltered.Items.All(l => l.Id != wholesale.Id), "filtering price lists by Purchase returns only purchase price lists");
    Check(unfilteredLists.TotalCount >= salesFiltered.TotalCount + purchaseFiltered.TotalCount && unfilteredLists.Items.Any(l => l.Id == vendorList.Id) && unfilteredLists.Items.Any(l => l.Id == wholesale.Id), "unfiltered price list page includes both Sales and Purchase");
    await Reject(async () => await lists.GetPagedAsync(new PriceListFilterQuery { Type = (PriceListType)99 }), "error.validation.request");
    Check(await lists.DeleteAsync(vendorList.Id), "temporary purchase price list is cleaned up");

    await Reject(async () => await lists.CreateVersionAsync(999999, new CreatePriceListVersionRequest { VersionCode = "V3", EffectiveFrom = from.AddDays(10) }), "error.validation.reference");
    await Reject(async () => await lists.CreateVersionAsync(wholesale.Id, new CreatePriceListVersionRequest { VersionCode = "V2", EffectiveFrom = from.AddDays(10) }), "error.sd.pricelist_duplicate_version");
    await Reject(async () => await lists.CreateVersionAsync(wholesale.Id, new CreatePriceListVersionRequest { VersionCode = "V3", EffectiveFrom = from.AddDays(10), EffectiveTo = from.AddDays(9) }), "error.validation.request");
    var draftV3 = await lists.CreateVersionAsync(wholesale.Id, new CreatePriceListVersionRequest { VersionCode = "v3", EffectiveFrom = from.AddDays(10) });
    Check(draftV3.VersionCode == "V3" && draftV3.Status == PriceListVersionStatus.Draft && draftV3.Items.Count == 0, "creating a price list version starts it in Draft status");
    var updatedV3 = await lists.UpdateVersionAsync(wholesale.Id, draftV3.Id, new UpdatePriceListVersionRequest { VersionCode = "v3-edit", EffectiveFrom = from.AddDays(10), EffectiveTo = from.AddDays(20) });
    Check(updatedV3.VersionCode == "V3-EDIT" && updatedV3.EffectiveTo == from.AddDays(20), "updating a Draft version updates its code and effective window");
    await Reject(async () => await lists.UpdateVersionAsync(wholesale.Id, draftV3.Id, new UpdatePriceListVersionRequest { VersionCode = "V2", EffectiveFrom = from.AddDays(10) }), "error.sd.pricelist_duplicate_version");
    await Reject(async () => await lists.UpdateVersionAsync(wholesale.Id, sourceVersion.Id, new UpdatePriceListVersionRequest { VersionCode = "V1-EDIT", EffectiveFrom = from }), "error.sd.pricelist_version_not_draft");

    var addedLine1 = await lists.AddVersionItemAsync(wholesale.Id, draftV3.Id, new CreatePriceListItemRequest { ItemId = item.Id, UnitPrice = 68, MinQuantity = 1, DiscountPercent = 0 });
    var addedLine2 = await lists.AddVersionItemAsync(wholesale.Id, draftV3.Id, new CreatePriceListItemRequest { ItemId = item.Id, UnitPrice = 58, MinQuantity = 10, DiscountPercent = 0 });
    Check(addedLine1.Id.HasValue && addedLine1.ItemCode == "ITEM" && addedLine1.UnitPrice == 68 && addedLine2.MinQuantity == 10, "adding items to a Draft version persists tiers and item details");
    await Reject(async () => await lists.AddVersionItemAsync(wholesale.Id, draftV3.Id, new CreatePriceListItemRequest { ItemId = item.Id, UnitPrice = 60, MinQuantity = 1 }), "error.sd.pricelist_item_duplicate");
    await Reject(async () => await lists.AddVersionItemAsync(wholesale.Id, draftV3.Id, new CreatePriceListItemRequest { ItemId = 999999, UnitPrice = 60, MinQuantity = 20 }), "error.inventory.item");
    await Reject(async () => await lists.AddVersionItemAsync(wholesale.Id, draftV3.Id, new CreatePriceListItemRequest { ItemId = item.Id, UnitPrice = 60, MinQuantity = 20, DiscountPercent = 101 }), "error.validation.number");
    await Reject(async () => await lists.AddVersionItemAsync(wholesale.Id, sourceVersion.Id, new CreatePriceListItemRequest { ItemId = item.Id, UnitPrice = 60, MinQuantity = 20 }), "error.sd.pricelist_version_not_draft");

    var updatedLine1 = await lists.UpdateVersionItemAsync(wholesale.Id, draftV3.Id, addedLine1.Id!.Value, new UpdatePriceListItemRequest { UnitPrice = 69, MinQuantity = 1, DiscountPercent = 5 });
    Check(updatedLine1.UnitPrice == 69 && updatedLine1.DiscountPercent == 5, "updating a Draft version item updates its price and discount");
    await Reject(async () => await lists.UpdateVersionItemAsync(wholesale.Id, draftV3.Id, addedLine1.Id!.Value, new UpdatePriceListItemRequest { UnitPrice = 69, MinQuantity = 10, DiscountPercent = 0 }), "error.sd.pricelist_item_duplicate");
    await Reject(async () => await lists.UpdateVersionItemAsync(wholesale.Id, draftV3.Id, 999999, new UpdatePriceListItemRequest { UnitPrice = 69, MinQuantity = 1 }), "error.validation.reference");
    await Reject(async () => await lists.UpdateVersionItemAsync(wholesale.Id, sourceVersion.Id, 1, new UpdatePriceListItemRequest { UnitPrice = 69, MinQuantity = 1 }), "error.sd.pricelist_version_not_draft");

    await lists.DeleteVersionItemAsync(wholesale.Id, draftV3.Id, addedLine2.Id!.Value);
    Check((await lists.GetVersionAsync(wholesale.Id, draftV3.Id)).Items.Count == 1, "deleting a Draft version item removes only that line");
    await Reject(async () => await lists.DeleteVersionItemAsync(wholesale.Id, draftV3.Id, addedLine2.Id!.Value), "error.validation.reference");
    await Reject(async () => await lists.DeleteVersionItemAsync(wholesale.Id, sourceVersion.Id, 1), "error.sd.pricelist_version_not_draft");

    var whileDraft = (await lists.CalculateAsync(new CalculatePriceRequest { CustomerId = customer.Id, BranchId = branch.Id, DocumentDate = from.AddDays(12), Items = [new() { ItemId = item.Id, Quantity = 1 }] })).Items.Single();
    Check(whileDraft.AppliedVersionCode == "V2" && whileDraft.UnitPrice == 72m, "Draft version does not take effect until activated");
    await Reject(async () => await lists.CloseVersionAsync(wholesale.Id, draftV3.Id), "error.sd.pricelist_version_invalid_status");

    var activatedV3 = await lists.ActivateVersionAsync(wholesale.Id, draftV3.Id);
    Check(activatedV3.Status == PriceListVersionStatus.Active, "activating a Draft version transitions it to Active");
    var whileActive = (await lists.CalculateAsync(new CalculatePriceRequest { CustomerId = customer.Id, BranchId = branch.Id, DocumentDate = from.AddDays(12), Items = [new() { ItemId = item.Id, Quantity = 1 }] })).Items.Single();
    Check(whileActive.AppliedVersionCode == "V3-EDIT" && whileActive.UnitPrice == 65.55m, "activated version takes effect immediately for matching document dates");
    await Reject(async () => await lists.ActivateVersionAsync(wholesale.Id, draftV3.Id), "error.sd.pricelist_version_invalid_status");
    await Reject(async () => await lists.UpdateVersionAsync(wholesale.Id, draftV3.Id, new UpdatePriceListVersionRequest { VersionCode = "V3-AGAIN", EffectiveFrom = from.AddDays(10) }), "error.sd.pricelist_version_not_draft");
    await Reject(async () => await lists.AddVersionItemAsync(wholesale.Id, draftV3.Id, new CreatePriceListItemRequest { ItemId = item.Id, UnitPrice = 50, MinQuantity = 5 }), "error.sd.pricelist_version_not_draft");
    await Reject(async () => await lists.UpdateVersionItemAsync(wholesale.Id, draftV3.Id, addedLine1.Id!.Value, new UpdatePriceListItemRequest { UnitPrice = 50, MinQuantity = 1 }), "error.sd.pricelist_version_not_draft");
    await Reject(async () => await lists.DeleteVersionItemAsync(wholesale.Id, draftV3.Id, addedLine1.Id!.Value), "error.sd.pricelist_version_not_draft");

    var closedV3 = await lists.CloseVersionAsync(wholesale.Id, draftV3.Id);
    Check(closedV3.Status == PriceListVersionStatus.Expired, "closing an Active version transitions it to Expired");
    var afterClose = (await lists.CalculateAsync(new CalculatePriceRequest { CustomerId = customer.Id, BranchId = branch.Id, DocumentDate = from.AddDays(12), Items = [new() { ItemId = item.Id, Quantity = 1 }] })).Items.Single();
    Check(afterClose.AppliedVersionCode == "V2" && afterClose.UnitPrice == 72m, "closing a version removes it from active pricing");
    await Reject(async () => await lists.CloseVersionAsync(wholesale.Id, draftV3.Id), "error.sd.pricelist_version_invalid_status");
    await Reject(async () => await lists.ActivateVersionAsync(wholesale.Id, draftV3.Id), "error.sd.pricelist_version_invalid_status");
    await Reject(async () => await lists.UpdateVersionAsync(wholesale.Id, draftV3.Id, new UpdatePriceListVersionRequest { VersionCode = "V3-EXP", EffectiveFrom = from.AddDays(10) }), "error.sd.pricelist_version_not_draft");
    await Reject(async () => await lists.AddVersionItemAsync(wholesale.Id, draftV3.Id, new CreatePriceListItemRequest { ItemId = item.Id, UnitPrice = 50, MinQuantity = 5 }), "error.sd.pricelist_version_not_draft");

    var archivedV4 = await lists.CreateVersionAsync(wholesale.Id, new CreatePriceListVersionRequest { VersionCode = "V4", EffectiveFrom = from.AddDays(30) });
    await db.PriceListVersions.Where(v => v.Id == archivedV4.Id).ExecuteUpdateAsync(v => v.SetProperty(x => x.Status, PriceListVersionStatus.Archived));
    await Reject(async () => await lists.UpdateVersionAsync(wholesale.Id, archivedV4.Id, new UpdatePriceListVersionRequest { VersionCode = "V4-EDIT", EffectiveFrom = from.AddDays(30) }), "error.sd.pricelist_version_not_draft");
    await Reject(async () => await lists.ActivateVersionAsync(wholesale.Id, archivedV4.Id), "error.sd.pricelist_version_invalid_status");
    await Reject(async () => await lists.CloseVersionAsync(wholesale.Id, archivedV4.Id), "error.sd.pricelist_version_invalid_status");
    await Reject(async () => await lists.AddVersionItemAsync(wholesale.Id, archivedV4.Id, new CreatePriceListItemRequest { ItemId = item.Id, UnitPrice = 10, MinQuantity = 1 }), "error.sd.pricelist_version_not_draft");
    Check(await lists.UpdateAsync(999999, new UpdatePriceListRequest { Code = "X", Name = "X", Currency = "VND", EffectiveFrom = from }) == null, "updating a missing price list returns nothing");
    await Reject(async () => await lists.DeleteAsync(wholesale.Id), "error.sd.pricelist_in_use");
    await db.Partners.Where(p => p.Id == customer.Id).ExecuteUpdateAsync(p => p.SetProperty(c => c.PriceListId, (int?)null));
    Check(await lists.DeleteAsync(wholesale.Id) && await lists.GetByIdAsync(wholesale.Id) == null && !await lists.DeleteAsync(wholesale.Id), "an unused price list can be deleted once");
    // Quotations: lifecycle, validity, conversion into a Draft order.
    var quotes = new SalesQuotationService(db, new PostingTransaction(db));
    var quoteDate = DateTime.UtcNow;
    var quoteAddress = await db.PartnerDeliveryAddresses.SingleAsync(a => a.PartnerId == customer.Id && !a.IsDefault);
    quoteAddress.RecipientName = "Quotation Receiver";
    quoteAddress.Phone = "0900000000";
    quoteAddress.Address = "10 Original Street";
    quoteAddress.Ward = "Original Ward";
    quoteAddress.District = "Original District";
    quoteAddress.City = "Original City";
    await db.SaveChangesAsync();
    CreateSalesQuotationRequest QuoteRequest(decimal quantity = 2, decimal discount = 20, DateTime? date = null, DateTime? until = null, int? deliveryAddressId = null) => new()
    {
        QuotationDate = date ?? quoteDate, ValidUntil = until ?? quoteDate.AddDays(7), CustomerId = customer.Id, BranchId = branch.Id,
        DeliveryAddressId = deliveryAddressId, TermsAndConditions = "Net 30",
        Items = [new() { ItemId = item.Id, Quantity = quantity, UnitPrice = 100, DiscountAmount = discount, Notes = "line" }]
    };
    var quote = await quotes.CreateAsync(QuoteRequest(deliveryAddressId: quoteAddress.Id));
    Check(System.Text.RegularExpressions.Regex.IsMatch(quote.QuotationNumber, @"^BG-\d{6}-\d{4,}$") && quote.Status == "Draft" && quote.SubTotal == 200 && quote.DiscountAmount == 20 && quote.TotalAmount == 180
        && quote.CustomerName == "Customer" && quote.Items.Single().ItemCode == "ITEM" && quote.Items.Single().LineTotal == 180
        && quote.DeliveryAddressId == quoteAddress.Id && quote.DeliveryRecipientName == "Quotation Receiver" && quote.DeliveryPhone == "0900000000"
        && quote.DeliveryAddressText == "10 Original Street, Original Ward, Original District, Original City",
        "quotation is numbered, priced and snapshots customer, lines and delivery address");
    await Reject(async () => await quotes.CreateAsync(new CreateSalesQuotationRequest { QuotationDate = quoteDate, ValidUntil = quoteDate, CustomerId = customer.Id, BranchId = branch.Id }), "error.validation.request");
    await Reject(async () => await quotes.CreateAsync(QuoteRequest(deliveryAddressId: int.MaxValue)), "error.sd.order_delivery_address_invalid");
    var toSupplier = QuoteRequest(); toSupplier.CustomerId = supplier.Id;
    await Reject(async () => await quotes.CreateAsync(toSupplier), "error.sales.customer");
    await Reject(async () => await quotes.CreateAsync(QuoteRequest(discount: 500)), "error.sales.discount");
    await Reject(async () => await quotes.CreateAsync(QuoteRequest(until: quoteDate.AddDays(-1))), "error.validation.request");
    var found = await quotes.GetPagedAsync(new BaseFilterQuery { Keyword = quote.QuotationNumber, Status = "Draft" });
    Check(found.TotalCount == 1 && found.Items.Single().Items.Count == 0 && found.Items.Single().BranchName == branch.Name, "quotation page filters by number and status and returns headers only");

    var sourceAddress = await db.PartnerDeliveryAddresses.SingleAsync(a => a.Id == quoteAddress.Id);
    sourceAddress.Address = "Changed Source Street";
    sourceAddress.Ward = "Changed Ward";
    await db.SaveChangesAsync();
    Check((await quotes.SendAsync(quote.Id)).Status == "Sent", "a draft quotation can be sent");
    await Reject(async () => await quotes.SendAsync(quote.Id), "error.sd.quotation_invalid_status");
    var revised = await quotes.UpdateAsync(quote.Id, new UpdateSalesQuotationRequest { QuotationDate = quoteDate, ValidUntil = quoteDate.AddDays(7), CustomerId = customer.Id, BranchId = branch.Id,
        DeliveryAddressId = quoteAddress.Id, Items = [new() { ItemId = item.Id, Quantity = 3, UnitPrice = 100 }] });
    Check(revised!.TotalAmount == 300 && revised.Items.Count == 1 && revised.Status == "Sent"
        && revised.DeliveryAddressText == "10 Original Street, Original Ward, Original District, Original City",
        "a sent quotation can be revised without refreshing its saved delivery address");
    Check((await quotes.AcceptAsync(quote.Id)).Status == "Accepted", "a sent quotation can be accepted");
    await Reject(async () => await quotes.UpdateAsync(quote.Id, new UpdateSalesQuotationRequest { QuotationDate = quoteDate, ValidUntil = quoteDate.AddDays(7), CustomerId = customer.Id, BranchId = branch.Id, Items = [new() { ItemId = item.Id, Quantity = 1, UnitPrice = 1 }] }), "error.sd.quotation_invalid_status");
    await Reject(async () => await quotes.DeleteAsync(quote.Id), "error.sd.quotation_invalid_status");

    var addressToDelete = await db.PartnerDeliveryAddresses.SingleAsync(a => a.Id == quoteAddress.Id);
    addressToDelete.IsDeleted = true;
    addressToDelete.DeletedAtUtc = DateTime.UtcNow;
    await db.SaveChangesAsync();
    var converted = await quotes.ConvertToOrderAsync(quote.Id);
    var convertedOrder = await db.SalesOrders.Include(o => o.Items).SingleAsync(o => o.Id == converted.SalesOrderId);
    Check(convertedOrder.Status == "Draft" && convertedOrder.TotalAmount == 300 && convertedOrder.Items.Single().Quantity == 3 && convertedOrder.CustomerId == customer.Id && convertedOrder.OrderNumber == converted.OrderNumber && convertedOrder.Notes == quote.QuotationNumber,
        "conversion creates a Draft order with the quotation lines");
    Check(convertedOrder.DeliveryAddressId == quoteAddress.Id && convertedOrder.DeliveryRecipientName == "Quotation Receiver"
        && convertedOrder.DeliveryPhone == "0900000000"
        && convertedOrder.DeliveryAddressText == "10 Original Street, Original Ward, Original District, Original City"
        && convertedOrder.DeliveryAddress == convertedOrder.DeliveryAddressText,
        "conversion carries the quotation address snapshot unchanged after the source address is deleted");
    var after = await quotes.GetByIdAsync(quote.Id);
    Check(after!.Status == "Converted" && after.ConvertedSalesOrderId == converted.SalesOrderId, "the converted quotation points at its order");
    await Reject(async () => await quotes.ConvertToOrderAsync(quote.Id), "error.sd.quotation_already_converted");
    await Reject(async () => await quotes.RejectAsync(quote.Id), "error.sd.quotation_invalid_status");

    var stale = await quotes.CreateAsync(QuoteRequest(date: quoteDate.AddDays(-10), until: quoteDate.AddDays(-5)));
    Check(stale.Status == "Expired" && (await quotes.GetPagedAsync(new BaseFilterQuery { Status = "Expired" })).Items.Any(q => q.Id == stale.Id) && !(await quotes.GetPagedAsync(new BaseFilterQuery { Status = "Draft" })).Items.Any(q => q.Id == stale.Id),
        "a quotation past its validity date reads as Expired");
    await Reject(async () => await quotes.SendAsync(stale.Id), "error.sd.quotation_expired");
    await Reject(async () => await quotes.ConvertToOrderAsync(stale.Id), "error.sd.quotation_expired");
    var extended = await quotes.UpdateAsync(stale.Id, new UpdateSalesQuotationRequest { QuotationDate = quoteDate, ValidUntil = quoteDate.AddDays(3), CustomerId = customer.Id, BranchId = branch.Id, Items = [new() { ItemId = item.Id, Quantity = 1, UnitPrice = 100 }] });
    Check(extended!.Status == "Draft", "an expired draft revives when its validity date is extended");

    var refused = await quotes.CreateAsync(QuoteRequest());
    await quotes.SendAsync(refused.Id);
    Check((await quotes.RejectAsync(refused.Id)).Status == "Rejected", "a sent quotation can be rejected");
    await Reject(async () => await quotes.ConvertToOrderAsync(refused.Id), "error.sd.quotation_invalid_status");
    await Reject(async () => await quotes.AcceptAsync(refused.Id), "error.sd.quotation_invalid_status");
    var scratch = await quotes.CreateAsync(QuoteRequest());
    Check(await quotes.DeleteAsync(scratch.Id) && await quotes.GetByIdAsync(scratch.Id) == null, "a draft quotation can be deleted");
    Check(PermissionCatalog.Contains("sd:quotation:view") && PermissionCatalog.Contains("sd:quotation:delete"), "quotation permissions are in the catalog");
    // Orders: credit check, stock reservation, credit approval and cancellation.
    var orders = new SalesOrderService(db, new PostingTransaction(db));
    Check(convertedOrder.QuotationId == quote.Id && convertedOrder.CustomerId == customer.Id, "a converted order remembers its quotation");
    var rsv = new Item { Code = "RSV", Name = "Reserved goods", BaseUomId = uom.Id, SalePrice = 100, CostPrice = 1 };
    var rich = new Partner { Code = "RICH", Name = "Rich", CreditLimit = 1000000, PartnerType = PartnerType.Customer };
    var poor = new Partner { Code = "POOR", Name = "Poor", CreditLimit = 0, PartnerType = PartnerType.Customer };
    var edge = new Partner { Code = "EDGE", Name = "Edge", CreditLimit = 1000, PartnerType = PartnerType.Customer };
    db.Items.Add(rsv); db.Partners.AddRange(rich, poor, edge); await db.SaveChangesAsync();
    db.StockBalances.Add(new StockBalance { WarehouseId = warehouse.Id, ItemId = rsv.Id, QuantityOnHand = 10, AverageCostPrice = 1, InventoryValue = 10 }); await db.SaveChangesAsync();
    async Task<decimal> Reserved() => (await db.StockBalances.AsNoTracking().SingleAsync(b => b.ItemId == rsv.Id)).QuantityReserved;
    async Task<SalesOrderDto> DraftOrder(int customerId, decimal quantity, decimal price = 100) => await orders.CreateAsync(new CreateSalesOrderRequest
    { BranchId = branch.Id, CustomerId = customerId, DeliveryAddress = "1 Main Street", Items = [new() { ItemId = rsv.Id, Quantity = quantity, UnitPrice = price }] });

    var draftOrder = await DraftOrder(rich.Id, 4);
    Check(draftOrder.BranchName == branch.Name && draftOrder.CreditCheckStatus == "Passed" && draftOrder.ReservationStatus == "None" && draftOrder.DeliveryAddress == "1 Main Street" && draftOrder.Items.Single().InvoicedQuantity == 0, "a new order returns its branch, delivery address and empty credit and reservation state");
    var first = await orders.ConfirmAsync(draftOrder.Id, default);
    Check(first.Status == "Confirmed" && first.CreditCheckStatus == "Passed" && first.ReservationStatus == "Reserved" && await Reserved() == 4, "confirming an order within credit reserves its stock");
    var big = await DraftOrder(rich.Id, 7);
    await Reject(async () => await orders.ConfirmAsync(big.Id, default), "error.sd.insufficient_available_stock");
    Check((await orders.GetByIdAsync(big.Id))!.Status == "Draft" && await Reserved() == 4 && !await db.StockReservations.AnyAsync(r => r.SalesOrderId == big.Id), "a stock shortage leaves the order Draft and reserves nothing");
    var pos = new CreateSalesInvoiceRequest { RequestId = Guid.NewGuid(), WarehouseId = warehouse.Id, CustomerId = rich.Id, PaidAmount = 800, Items = [new() { ItemId = rsv.Id, Quantity = 8, UnitPrice = 100 }] };
    await Reject(async () => await Sales(db).CreateInvoiceAsync(pos), "error.stock.insufficient");

    var cancelled = await orders.CancelAsync(first.Id, "customer changed mind", default);
    Check(cancelled.Status == "Cancelled" && cancelled.ReservationStatus == "Released" && await Reserved() == 0 && !await db.StockReservations.AnyAsync(r => r.SalesOrderId == first.Id && r.Status == "Active"), "cancelling an order releases its reserved stock");
    await Reject(async () => await orders.CancelAsync(first.Id, "again", default), "error.sd.order_invalid_status_for_action");
    await Reject(async () => await orders.CancelAsync(big.Id, " ", default), "error.validation.request");
    var cancelledDraft = await orders.CancelAsync(big.Id, "not needed", default);
    Check(cancelledDraft.Status == "Cancelled" && cancelledDraft.ReservationStatus == "None", "a Draft order can be cancelled without any stock to release");

    var held = await orders.ConfirmAsync((await DraftOrder(poor.Id, 2)).Id, default);
    Check(held.Status == "PendingApproval" && held.CreditCheckStatus == "PendingApproval" && held.ReservationStatus == "None" && await Reserved() == 0, "an order over the credit limit waits for approval and holds no stock");
    Check((await orders.GetPagedAsync(new BaseFilterQuery { Status = "PendingApproval" })).Items.Any(o => o.Id == held.Id), "the order page filters by status");
    await Reject(async () => await orders.ApproveCreditAsync(held.Id, "", default), "error.validation.request");
    var approved = await orders.ApproveCreditAsync(held.Id, "Paid in advance", default);
    Check(approved.Status == "Confirmed" && approved.CreditCheckStatus == "Overridden" && approved.ReservationStatus == "Reserved" && await Reserved() == 2, "approving credit confirms the order and reserves its stock");
    await Reject(async () => await orders.ApproveCreditAsync(held.Id, "again", default), "error.sd.order_invalid_status_for_action");
    await Reject(async () => await orders.ApproveCreditAsync(999999, "x", default), "error.order.not_found");

    var warned = await orders.ConfirmAsync((await DraftOrder(edge.Id, 2, 450)).Id, default);
    Check(warned.Status == "Confirmed" && warned.CreditCheckStatus == "Warning" && await Reserved() == 4, "an order close to the credit limit is confirmed with a warning");
    var second = await orders.ConfirmAsync((await DraftOrder(edge.Id, 1, 200)).Id, default);
    Check(second.Status == "PendingApproval", "orders already confirmed for a customer count against its credit limit");
    // Unlimited credit: one shared policy lifts the limit for orders and invoices, and raises no warning.
    var unlimited = new Partner { Code = "UNLIM", Name = "Unlimited", CreditLimit = 0, IsUnlimitedCredit = true, PartnerType = PartnerType.Customer };
    var limitedTwin = new Partner { Code = "LTWIN", Name = "Limited twin", CreditLimit = 0, PartnerType = PartnerType.Customer };
    var creditItem = new Item { Code = "CREDIT", Name = "Credit goods", BaseUomId = uom.Id, SalePrice = 100, CostPrice = 1 };
    db.Partners.AddRange(unlimited, limitedTwin); db.Items.Add(creditItem); await db.SaveChangesAsync();
    db.StockBalances.Add(new StockBalance { WarehouseId = warehouse.Id, ItemId = creditItem.Id, QuantityOnHand = 10, AverageCostPrice = 1, InventoryValue = 10 }); await db.SaveChangesAsync();
    Check(CreditLimitPolicy.Evaluate(true, 100, 5000) == CreditLevel.Passed && CreditLimitPolicy.Evaluate(false, 100, 90) == CreditLevel.Warning
        && CreditLimitPolicy.Evaluate(false, 100, 101) == CreditLevel.Exceeded && CreditLimitPolicy.Evaluate(false, 100, 10) == CreditLevel.Passed, "the credit policy lifts the limit only for unlimited customers");
    var unlimitedOrder = await orders.ConfirmAsync((await DraftOrder(unlimited.Id, 1)).Id, default);
    Check(unlimitedOrder.Status == "Confirmed" && unlimitedOrder.CreditCheckStatus == "Passed" && unlimitedOrder.ReservationStatus == "Reserved", "an order of a customer with unlimited credit is confirmed without approval or warning");
    await orders.CancelAsync(unlimitedOrder.Id, "credit test done", default);
    (await db.Accounts.SingleAsync(a => a.Code == "511")).IsActive = true; await db.SaveChangesAsync();   // earlier checks switched revenue off
    CreateSalesInvoiceRequest OnCredit(int customerId) => new() { RequestId = Guid.NewGuid(), WarehouseId = warehouse.Id, CustomerId = customerId, PaidAmount = 0, Items = [new() { ItemId = creditItem.Id, Quantity = 1, UnitPrice = 100 }] };
    await Reject(async () => await Sales(db).CreateInvoiceAsync(OnCredit(limitedTwin.Id)), "error.sales.credit_limit");
    var unlimitedInvoice = await Sales(db).CreateInvoiceAsync(OnCredit(unlimited.Id));
    Check(unlimitedInvoice.RemainingDebt == 100 && (await db.Partners.AsNoTracking().SingleAsync(p => p.Id == unlimited.Id)).DebtBalance == 100, "an invoice on credit is posted for an unlimited customer and its debt is still recorded");

    // Changing the flag needs its own right, tells the other holders of that right and ignores a repeated value.
    Check(PermissionCatalog.Contains("crm:partner:unlimited-credit"), "the permission catalog lists the unlimited credit right");
    var holderA = new AppUser { UserName = "UNL_A", DisplayName = "Holder A", Role = "Viewer" };
    var holderB = new AppUser { UserName = "UNL_B", DisplayName = "Holder B", Role = "Viewer" };
    var withoutRight = new AppUser { UserName = "UNL_C", DisplayName = "No right", Role = "Viewer" };
    db.AppUsers.AddRange(holderA, holderB, withoutRight); await db.SaveChangesAsync();
    IamPrincipal Rights(params string[] actions) => new([SystemRoles.Allow("Test", actions)], new OrgContext { CompanyId = 1 }, false);
    var iamStub = new IamStub(new() { [holderA.Id] = Rights("crm:partner:unlimited-credit"), [holderB.Id] = Rights("crm:partner:unlimited-credit"), [withoutRight.Id] = Rights("crm:partner:create") });
    var partnerEvents = new RecordingEngine();
    PartnerService PartnersAs(AppUser who) { iamStub.CurrentUserId = who.Id; return new PartnerService(db, new ActorUser(who.Id), iamStub, partnerEvents); }
    await Reject(async () => await PartnersAs(withoutRight).SetUnlimitedCreditAsync(limitedTwin.Id, true), "error.crm.unlimited_credit_forbidden");
    await Reject(async () => await PartnersAs(withoutRight).CreatePartnerAsync(new CreatePartnerRequest { Code = "NORIGHT", Name = "No right", IsUnlimitedCredit = true }), "error.crm.unlimited_credit_forbidden");
    var plain = await PartnersAs(withoutRight).CreatePartnerAsync(new CreatePartnerRequest { Code = "PLAIN", Name = "Plain" });
    Check(!plain.IsUnlimitedCredit && partnerEvents.Events.Count == 0, "a user without the right can still create a partner with limited credit and nobody is notified");
    var switchedOn = await PartnersAs(holderA).SetUnlimitedCreditAsync(limitedTwin.Id, true);
    var creditNotices = partnerEvents.Events.OfType<ERPm.Domain.Events.NotificationEvent>().ToList();
    Check(switchedOn.IsUnlimitedCredit && creditNotices.Count == 1 && creditNotices[0].TemplateCode == "CRM_PARTNER_UNLIMITED_CREDIT_CHANGED" && creditNotices[0].TargetId == holderB.Id, "switching unlimited credit on notifies the other holders of the right but not the actor");
    await PartnersAs(holderA).SetUnlimitedCreditAsync(limitedTwin.Id, true);
    Check(partnerEvents.Events.Count == 1, "repeating the same value changes nothing and sends no notification");
    await Reject(async () => await PartnersAs(holderA).SetUnlimitedCreditAsync(limitedTwin.Id, null), "error.crm.unlimited_credit_value_required");
    Check(partnerEvents.Events.Count == 1 && (await db.Partners.AsNoTracking().SingleAsync(p => p.Id == limitedTwin.Id)).IsUnlimitedCredit, "a request without the value leaves the flag on and sends no notification");
    var twinInvoice = await Sales(db).CreateInvoiceAsync(OnCredit(limitedTwin.Id));
    Check(twinInvoice.RemainingDebt == 100, "once switched on, the customer's credit invoices are no longer blocked");
    await PartnersAs(holderB).SetUnlimitedCreditAsync(limitedTwin.Id, false);
    await Reject(async () => await Sales(db).CreateInvoiceAsync(OnCredit(limitedTwin.Id)), "error.sales.credit_limit");
    await Reject(async () => await PartnersAs(holderA).SetUnlimitedCreditAsync(999999, true), "error.crm.partner_not_found");
    (await db.Accounts.SingleAsync(a => a.Code == "511")).IsActive = false; await db.SaveChangesAsync();   // restore the state the next checks expect

    // Reading and editing one partner.
    var viewed = await PartnersAs(holderA).GetPartnerAsync(limitedTwin.Id);
    Check(viewed.Id == limitedTwin.Id && viewed.Code == "LTWIN" && viewed.Name == "Limited twin" && !viewed.IsUnlimitedCredit, "one partner can be read by id");
    await Reject(async () => await PartnersAs(holderA).GetPartnerAsync(999999), "error.crm.partner_not_found");
    var activeList = await db.PriceLists.AsNoTracking().FirstAsync(l => l.IsActive);
    var debtBefore = (await db.Partners.AsNoTracking().SingleAsync(p => p.Id == limitedTwin.Id)).DebtBalance;
    var partnerEdit = await PartnersAs(holderA).UpdatePartnerAsync(limitedTwin.Id, new UpdatePartnerRequest
    { Name = "  Twin renamed ", PartnerType = PartnerType.Customer, Phone = "0901234567", Email = "twin@example.test", Address = "9 Edit Road", TaxCode = "0312345678", CreditLimit = 5000, PriceListId = activeList.Id, Notes = "VIP", IsActive = true });
    var storedTwin = await db.Partners.AsNoTracking().SingleAsync(p => p.Id == limitedTwin.Id);
    Check(partnerEdit.Name == "Twin renamed" && partnerEdit.Phone == "0901234567" && partnerEdit.Email == "twin@example.test" && partnerEdit.Address == "9 Edit Road" && partnerEdit.TaxCode == "0312345678"
        && partnerEdit.CreditLimit == 5000 && partnerEdit.PriceListId == activeList.Id && partnerEdit.Notes == "VIP", "editing a partner saves its descriptive fields, credit limit and price list");
    Check(storedTwin.Code == "LTWIN" && storedTwin.DebtBalance == debtBefore && !storedTwin.IsUnlimitedCredit, "editing never changes the code, the debt balance or the unlimited credit flag");
    Check(storedTwin.LastModifiedBy != null && storedTwin.LastModifiedAtUtc != null, "the editor and the time are recorded");
    UpdatePartnerRequest Edit(string name = "Twin", Action<UpdatePartnerRequest>? change = null) { var e = new UpdatePartnerRequest { Name = name, PartnerType = PartnerType.Customer, CreditLimit = 0, IsActive = true }; change?.Invoke(e); return e; }
    var partnerBlanked = await PartnersAs(holderA).UpdatePartnerAsync(limitedTwin.Id, Edit(change: e => { e.Phone = " "; e.Notes = ""; e.PriceListId = activeList.Id; e.IsActive = false; }));
    Check(partnerBlanked.Phone == null && partnerBlanked.Notes == null && !partnerBlanked.IsActive && partnerBlanked.CreditLimit == 0, "blank optional fields are cleared and the partner can be deactivated");
    await Reject(async () => await PartnersAs(holderA).UpdatePartnerAsync(limitedTwin.Id, Edit(" ")), "error.validation.request");
    await Reject(async () => await PartnersAs(holderA).UpdatePartnerAsync(limitedTwin.Id, Edit(change: e => e.Phone = new string('1', 51))), "error.validation.request");
    await Reject(async () => await PartnersAs(holderA).UpdatePartnerAsync(limitedTwin.Id, Edit(change: e => e.CreditLimit = -1)), "error.validation.number");
    await Reject(async () => await PartnersAs(holderA).UpdatePartnerAsync(limitedTwin.Id, Edit(change: e => e.PriceListId = 999999)), "error.validation.reference");
    await Reject(async () => await PartnersAs(holderA).UpdatePartnerAsync(limitedTwin.Id, Edit(change: e => e.PartnerType = (PartnerType)99)), "error.validation.request");
    await Reject(async () => await PartnersAs(holderA).UpdatePartnerAsync(999999, Edit("X")), "error.crm.partner_not_found");
    // A body that leaves out a required field must change nothing (it used to reset the limit and the active flag).
    var beforeIncomplete = await db.Partners.AsNoTracking().SingleAsync(p => p.Id == limitedTwin.Id);
    await Reject(async () => await PartnersAs(holderA).UpdatePartnerAsync(limitedTwin.Id, new UpdatePartnerRequest { Name = "Only name" }), "error.validation.request");
    await Reject(async () => await PartnersAs(holderA).UpdatePartnerAsync(limitedTwin.Id, Edit(change: e => e.CreditLimit = null)), "error.validation.request");
    await Reject(async () => await PartnersAs(holderA).UpdatePartnerAsync(limitedTwin.Id, Edit(change: e => e.IsActive = null)), "error.validation.request");
    await Reject(async () => await PartnersAs(holderA).UpdatePartnerAsync(limitedTwin.Id, Edit(change: e => e.PartnerType = null)), "error.validation.request");
    var afterIncomplete = await db.Partners.AsNoTracking().SingleAsync(p => p.Id == limitedTwin.Id);
    Check(afterIncomplete.Name == beforeIncomplete.Name && afterIncomplete.CreditLimit == beforeIncomplete.CreditLimit && afterIncomplete.IsActive == beforeIncomplete.IsActive && afterIncomplete.PartnerType == beforeIncomplete.PartnerType, "an incomplete edit changes nothing");
    await Reject(async () => await PartnersAs(holderA).UpdatePartnerAsync(limitedTwin.Id, Edit(change: e => e.Email = "not-an-email")), "error.validation.request");
    await Reject(async () => await PartnersAs(holderA).UpdatePartnerAsync(limitedTwin.Id, Edit(change: e => e.Email = "a@b@c.test")), "error.validation.request");

    // Delivery addresses: default rules, limit, validation and soft delete.
    var addressee = new Partner { Code = "ADDR", Name = "Addressee", PartnerType = PartnerType.Customer };
    db.Partners.Add(addressee); await db.SaveChangesAsync();
    PartnerDeliveryAddressService Addresses(ApplicationDbContext c) => new(c, new PostingTransaction(c));
    SaveDeliveryAddressRequest Address(string name, bool isDefault = false, string? phone = null) => new() { RecipientName = name, Address = name + " Street", Phone = phone, City = "Hanoi", IsDefault = isDefault };
    var addresses = Addresses(db);
    var firstAddress = await addresses.CreateAsync(addressee.Id, Address("One"));
    Check(firstAddress.IsDefault && firstAddress.PartnerId == addressee.Id && firstAddress.Phone == null && firstAddress.CreatedAt.Kind == DateTimeKind.Utc, "the first address of a partner becomes its default");
    var secondAddress = await addresses.CreateAsync(addressee.Id, Address("Two", isDefault: true, phone: "+84 (90) 123-4567"));
    var listedAddresses = await addresses.GetAsync(addressee.Id);
    Check(secondAddress.IsDefault && secondAddress.Phone == "+84901234567" && listedAddresses.Select(a => a.RecipientName).SequenceEqual(["Two", "One"]) && listedAddresses.Count(a => a.IsDefault) == 1, "a new default replaces the old one and the phone is stored normalized, default first");
    var thirdAddress = await addresses.CreateAsync(addressee.Id, Address("Three"));
    Check(!thirdAddress.IsDefault, "a later address is not the default unless asked");
    var lowered = await addresses.UpdateAsync(addressee.Id, secondAddress.Id, Address("Two", isDefault: false));
    Check(lowered.IsDefault, "a default address cannot be lowered, another one must be chosen");
    var renamed = await addresses.UpdateAsync(addressee.Id, thirdAddress.Id, Address("Three renamed", isDefault: true));
    Check(renamed.IsDefault && !(await addresses.GetByIdAsync(addressee.Id, secondAddress.Id)).IsDefault, "updating with the default flag moves the default in one step");
    var again = await addresses.SetDefaultAsync(addressee.Id, firstAddress.Id);
    var repeated = await addresses.SetDefaultAsync(addressee.Id, firstAddress.Id);
    Check(again.IsDefault && repeated.IsDefault && (await addresses.GetAsync(addressee.Id)).Count(a => a.IsDefault) == 1, "set-default is atomic and idempotent");
    await using (var left = Db(user)) await using (var right = Db(user))
    {
        await Task.WhenAll(Addresses(left).SetDefaultAsync(addressee.Id, secondAddress.Id), Addresses(right).SetDefaultAsync(addressee.Id, thirdAddress.Id));
        Check((await addresses.GetAsync(addressee.Id)).Count(a => a.IsDefault) == 1, "two simultaneous set-default requests still leave a single default");
    }
    var defaultNow = (await addresses.GetAsync(addressee.Id)).Single(a => a.IsDefault);
    await addresses.DeleteAsync(addressee.Id, defaultNow.Id);
    var afterDelete = await addresses.GetAsync(addressee.Id);
    var expectedNext = afterDelete.OrderBy(a => a.CreatedAt).ThenBy(a => a.Id).First();
    Check(afterDelete.Count == 2 && afterDelete.Count(a => a.IsDefault) == 1 && afterDelete.Single(a => a.IsDefault).Id == expectedNext.Id && afterDelete.All(a => a.Id != defaultNow.Id), "deleting the default moves it to the oldest remaining address");
    Check((await db.PartnerDeliveryAddresses.IgnoreQueryFilters().AsNoTracking().SingleAsync(a => a.Id == defaultNow.Id)).IsDeleted, "a deleted address stays in the table as a soft delete");
    await Reject(async () => await addresses.GetByIdAsync(addressee.Id, defaultNow.Id), "error.crm.delivery_address_not_found");
    await Reject(async () => await addresses.GetByIdAsync(customer.Id, afterDelete[0].Id), "error.crm.delivery_address_not_found");
    await Reject(async () => await addresses.GetAsync(999999), "error.crm.partner_not_found");
    await Reject(async () => await addresses.CreateAsync(addressee.Id, Address(" ")), "error.crm.delivery_address_recipient_required");
    await Reject(async () => await addresses.CreateAsync(addressee.Id, new SaveDeliveryAddressRequest { RecipientName = "X", Address = " " }), "error.crm.delivery_address_address_required");
    await Reject(async () => await addresses.CreateAsync(addressee.Id, Address("Bad phone", phone: "12ab")), "error.crm.delivery_address_phone_invalid");
    await Reject(async () => await addresses.CreateAsync(addressee.Id, Address("Short phone", phone: "1234567")), "error.crm.delivery_address_phone_invalid");
    await Reject(async () => await addresses.CreateAsync(addressee.Id, Address(new string('r', 256))), "error.crm.delivery_address_recipient_too_long");
    await Reject(async () => await addresses.CreateAsync(addressee.Id, new SaveDeliveryAddressRequest { RecipientName = "X", Address = new string('a', 501) }), "error.crm.delivery_address_address_too_long");
    await Reject(async () => await addresses.CreateAsync(addressee.Id, new SaveDeliveryAddressRequest { RecipientName = "X", Address = "A", City = new string('c', 101) }), "error.crm.delivery_address_city_too_long");
    await Reject(async () => await addresses.CreateAsync(addressee.Id, new SaveDeliveryAddressRequest { RecipientName = "X", Address = "A", District = new string('d', 101) }), "error.crm.delivery_address_district_too_long");
    await Reject(async () => await addresses.CreateAsync(addressee.Id, new SaveDeliveryAddressRequest { RecipientName = "X", Address = "A", Ward = new string('w', 101) }), "error.crm.delivery_address_ward_too_long");
    for (var n = afterDelete.Count; n < PartnerDeliveryAddressService.MaxAddressesPerPartner; n++) await addresses.CreateAsync(addressee.Id, Address("Fill " + n));
    await Reject(async () => await addresses.CreateAsync(addressee.Id, Address("One too many")), "error.crm.delivery_address_limit");
    await addresses.DeleteAsync(addressee.Id, (await addresses.GetAsync(addressee.Id)).Last().Id);
    Check((await addresses.CreateAsync(addressee.Id, Address("Fits again"))).PartnerId == addressee.Id, "deleting an address frees a slot under the limit");
    // Order VAT per line, delivery time and the copied delivery address.
    SalesOrderItemDto Line(SalesOrderDto order, int itemId) => order.Items.Single(i => i.ItemId == itemId);
    CreateSalesOrderRequest VatOrder(int customerId, decimal vatA, decimal vatB, decimal headerDiscount = 20, decimal headerTax = 0) => new()
    {
        BranchId = branch.Id, CustomerId = customerId, DiscountAmount = headerDiscount, TaxAmount = headerTax,
        Items = [new() { ItemId = rsv.Id, Quantity = 3, UnitPrice = 100, DiscountAmount = 30, VatRate = vatA }, new() { ItemId = creditItem.Id, Quantity = 1, UnitPrice = 33.3333m, VatRate = vatB }]
    };
    var vatOrder = await orders.CreateAsync(VatOrder(rich.Id, 8, 10));
    var vatLineA = decimal.Round(270m * 8m / 100m, 4, MidpointRounding.AwayFromZero);
    var vatLineB = decimal.Round(33.3333m * 10m / 100m, 4, MidpointRounding.AwayFromZero);
    Check(Line(vatOrder, rsv.Id).LineTotal == 270 && Line(vatOrder, rsv.Id).TaxAmount == vatLineA && Line(vatOrder, creditItem.Id).TaxAmount == vatLineB && Line(vatOrder, rsv.Id).VatRate == 8,
        "each order line carries its VAT rate and the tax worked out on the amount after the line discount");
    Check(vatOrder.SubTotal == 303.3333m && vatOrder.TaxAmount == vatLineA + vatLineB && vatOrder.TotalAmount == vatOrder.SubTotal - 20 + vatOrder.TaxAmount, "the order total is subtotal minus discount plus the VAT of all lines, with no rounding drift");
    var echoed = await orders.CreateAsync(VatOrder(rich.Id, 8, 10, headerTax: vatLineA + vatLineB));
    Check(echoed.TaxAmount == vatOrder.TaxAmount, "a client may send back the derived header VAT");
    await Reject(async () => await orders.CreateAsync(VatOrder(rich.Id, 8, 10, headerTax: 1)), "error.validation.request");
    await Reject(async () => await orders.CreateAsync(VatOrder(rich.Id, 101, 0)), "error.sd.order_vat_rate_invalid");
    await Reject(async () => await orders.CreateAsync(VatOrder(rich.Id, -1, 0)), "error.sd.order_vat_rate_invalid");
    await Reject(async () => await orders.CreateAsync(VatOrder(rich.Id, 8.555m, 0)), "error.sd.order_vat_rate_invalid");
    var noVat = await orders.CreateAsync(VatOrder(rich.Id, 0, 0));
    Check(noVat.TaxAmount == 0 && noVat.TotalAmount == noVat.SubTotal - 20, "lines without VAT leave the order tax at zero");
    var revatted = await orders.UpdateAsync(noVat.Id, new UpdateSalesOrderRequest { BranchId = branch.Id, CustomerId = rich.Id, DiscountAmount = 20, Items = VatOrder(rich.Id, 5, 0).Items });
    Check(revatted!.TaxAmount == decimal.Round(270m * 5m / 100m, 4, MidpointRounding.AwayFromZero) && revatted.TotalAmount == revatted.SubTotal - 20 + revatted.TaxAmount, "editing an order recomputes its tax and total");

    var deliveryAt = new DateTime(2026, 11, 10, 8, 30, 0, DateTimeKind.Utc);
    var timed = await orders.CreateAsync(new CreateSalesOrderRequest { BranchId = branch.Id, CustomerId = rich.Id, ExpectedDeliveryDate = deliveryAt, Items = [new() { ItemId = rsv.Id, Quantity = 1, UnitPrice = 100 }] });
    await using (var reread = Db(user))
    {
        var storedDelivery = (await reread.SalesOrders.AsNoTracking().SingleAsync(o => o.Id == timed.Id)).ExpectedDeliveryDate;
        Check(storedDelivery == deliveryAt && storedDelivery.Value.Kind == DateTimeKind.Utc && timed.ExpectedDeliveryDate == deliveryAt, "the expected delivery keeps hours and minutes in UTC");
    }
    await Reject(async () => await orders.CreateAsync(new CreateSalesOrderRequest { BranchId = branch.Id, CustomerId = rich.Id, ExpectedDeliveryDate = new DateTime(2026, 11, 10, 8, 30, 0, DateTimeKind.Unspecified), Items = [new() { ItemId = rsv.Id, Quantity = 1, UnitPrice = 100 }] }), "error.validation.request");

    var pickable = (await addresses.GetAsync(addressee.Id)).First();
    CreateSalesOrderRequest ToAddress(int? addressId, int customerId) => new() { BranchId = branch.Id, CustomerId = customerId, DeliveryAddressId = addressId, Items = [new() { ItemId = rsv.Id, Quantity = 1, UnitPrice = 100 }] };
    var shipped = await orders.CreateAsync(ToAddress(pickable.Id, addressee.Id));
    var expectedText = string.Join(", ", new[] { pickable.Address, pickable.Ward, pickable.District, pickable.City }.Where(x => !string.IsNullOrWhiteSpace(x)));
    Check(shipped.DeliveryAddressId == pickable.Id && shipped.DeliveryRecipientName == pickable.RecipientName && shipped.DeliveryPhone == pickable.Phone && shipped.DeliveryAddressText == expectedText && shipped.DeliveryAddress == expectedText,
        "an order keeps a copy of the chosen delivery address");
    await addresses.UpdateAsync(addressee.Id, pickable.Id, Address("Renamed after the order"));
    await addresses.DeleteAsync(addressee.Id, pickable.Id);
    var shippedAgain = (await orders.GetByIdAsync(shipped.Id))!;
    Check(shippedAgain.DeliveryRecipientName == shipped.DeliveryRecipientName && shippedAgain.DeliveryAddressText == expectedText, "changing or deleting the source address leaves the order's copy unchanged");
    var ownerless = (await addresses.GetAsync(addressee.Id)).First();
    await Reject(async () => await orders.CreateAsync(ToAddress(ownerless.Id, rich.Id)), "error.sd.order_delivery_address_invalid");
    await Reject(async () => await orders.CreateAsync(ToAddress(pickable.Id, addressee.Id)), "error.sd.order_delivery_address_invalid");
    var resaved = await orders.UpdateAsync(shipped.Id, new UpdateSalesOrderRequest { BranchId = branch.Id, CustomerId = addressee.Id, DeliveryAddressId = pickable.Id, Notes = "edited", Items = [new() { ItemId = rsv.Id, Quantity = 2, UnitPrice = 100 }] });
    Check(resaved!.DeliveryAddressId == pickable.Id && resaved.DeliveryRecipientName == shipped.DeliveryRecipientName && resaved.DeliveryAddressText == expectedText && resaved.DeliveryAddress == expectedText && resaved.Notes == "edited",
        "an order can be saved again with the address it already holds after that address was deleted, keeping its copy");
    await Reject(async () => await orders.UpdateAsync(shipped.Id, new UpdateSalesOrderRequest { BranchId = branch.Id, CustomerId = rich.Id, DeliveryAddressId = pickable.Id, Items = [new() { ItemId = rsv.Id, Quantity = 1, UnitPrice = 100 }] }), "error.sd.order_delivery_address_invalid");
    var cleared = await orders.UpdateAsync(shipped.Id, new UpdateSalesOrderRequest { BranchId = branch.Id, CustomerId = addressee.Id, DeliveryAddress = "Typed by hand", Items = [new() { ItemId = rsv.Id, Quantity = 1, UnitPrice = 100 }] });
    Check(cleared!.DeliveryAddressId == null && cleared.DeliveryRecipientName == null && cleared.DeliveryAddressText == null && cleared.DeliveryAddress == "Typed by hand", "dropping the address choice clears the copy");

    // Light lists: a summary request returns the same rows without their lines, filtered and sorted on the server.
    var manyLines = await orders.CreateAsync(new CreateSalesOrderRequest { BranchId = branch.Id, CustomerId = rich.Id, Items = Enumerable.Range(0, 40).Select(n => new CreateSalesOrderItemRequest { ItemId = rsv.Id, Quantity = 1, UnitPrice = 100 + n }).ToList() });
    var fullList = await orders.GetPagedAsync(new BaseFilterQuery { PageSize = 200 });
    var summaryList = await orders.GetPagedAsync(new BaseFilterQuery { PageSize = 200, Summary = true });
    var fullRow = fullList.Items.Single(o => o.Id == manyLines.Id);
    var summaryRow = summaryList.Items.Single(o => o.Id == manyLines.Id);
    Check(fullRow.Items.Count == 40 && summaryRow.Items.Count == 0 && summaryRow.OrderNumber == fullRow.OrderNumber && summaryRow.TotalAmount == fullRow.TotalAmount && summaryRow.CustomerName == fullRow.CustomerName && summaryList.TotalCount == fullList.TotalCount,
        "an order list asked for as a summary keeps every column but carries no lines");
    var fullBytes = JsonSerializer.SerializeToUtf8Bytes(fullList).Length; var summaryBytes = JsonSerializer.SerializeToUtf8Bytes(summaryList).Length;
    Check(summaryBytes < fullBytes / 2, $"the summary list is much smaller than the full list ({summaryBytes} against {fullBytes} bytes)");
    Console.WriteLine($"MEASURE order list payload: full {fullBytes} bytes, summary {summaryBytes} bytes, {fullList.Items.Count} rows");
    var today = DateTime.UtcNow.Date;
    var inRange = await orders.GetPagedAsync(new BaseFilterQuery { PageSize = 200, Summary = true, From = today, To = today });
    var outOfRange = await orders.GetPagedAsync(new BaseFilterQuery { PageSize = 200, Summary = true, From = today.AddDays(-30), To = today.AddDays(-10) });
    Check(inRange.Items.Any(o => o.Id == manyLines.Id) && outOfRange.TotalCount == 0, "orders can be filtered by date range on the server");
    var byKeyword = await orders.GetPagedAsync(new BaseFilterQuery { Q = manyLines.OrderNumber, Summary = true });
    Check(byKeyword.Items.Count == 1 && byKeyword.Items[0].Id == manyLines.Id, "the q shortcut searches like the keyword");
    var descending = await orders.GetPagedAsync(new BaseFilterQuery { Sort = "-OrderNumber", PageSize = 5, Summary = true });
    var ascending = await orders.GetPagedAsync(new BaseFilterQuery { Sort = "OrderNumber", PageSize = 5, Summary = true });
    Check(descending.Items.Select(o => o.OrderNumber).SequenceEqual(descending.Items.Select(o => o.OrderNumber).OrderByDescending(n => n)) && ascending.Items.Select(o => o.OrderNumber).SequenceEqual(ascending.Items.Select(o => o.OrderNumber).OrderBy(n => n)) && descending.Items[0].Id != ascending.Items[0].Id, "a leading minus in sort reverses the order");
    var listPage2 = await orders.GetPagedAsync(new BaseFilterQuery { PageIndex = 2, PageSize = 3, Summary = true });
    Check(listPage2.PageIndex == 2 && listPage2.Items.Count <= 3 && listPage2.TotalCount == summaryList.TotalCount, "a list is paged on the server");
    var quoteSummary = await quotes.GetPagedAsync(new BaseFilterQuery { Summary = true, From = today, To = today });
    Check(quoteSummary.TotalCount > 0 && quoteSummary.Items.All(q => q.Items.Count == 0), "quotations accept the same date filter and carry no lines in a list");
    var farFuture = await orders.GetPagedAsync(new BaseFilterQuery { PageSize = 200, Summary = true, From = DateTime.MinValue, To = DateTime.MaxValue });
    Check(farFuture.TotalCount == summaryList.TotalCount, "a date range ending on the last day of the calendar does not overflow");
    var camel = await orders.GetPagedAsync(new BaseFilterQuery { Sort = "-orderNumber", PageSize = 5, Summary = true });
    Check(camel.Items.Select(o => o.OrderNumber).SequenceEqual(descending.Items.Select(o => o.OrderNumber)), "sort accepts the JSON field name in camel case");
    var sameKey = await orders.GetPagedAsync(new BaseFilterQuery { Sort = "status", PageSize = 3, Summary = true });
    var sameKeyNext = await orders.GetPagedAsync(new BaseFilterQuery { Sort = "status", PageIndex = 2, PageSize = 3, Summary = true });
    Check(sameKey.Items.Select(o => o.Id).Intersect(sameKeyNext.Items.Select(o => o.Id)).Count() == 0, "sorting on a column with equal values never repeats a row across pages");
    await db.SalesOrderItems.Where(i => i.SalesOrderId == warned.Id).ExecuteUpdateAsync(i => i.SetProperty(x => x.InvoicedQuantity, 1m));
    await Reject(async () => await orders.CancelAsync(warned.Id, "too late", default), "error.sd.cannot_cancel_invoiced_order");
    // Tester findings: F1 pricing outside the data scope, F2 deleting a list used by an order, F3 order state errors.
    var fixList = await lists.CreateAsync(ListRequest("FIXPL", 80));
    await using (var outsider = Db(new TestUser(false, [999999])))
    {
        var scoped = await new PriceListService(outsider).CalculateAsync(new CalculatePriceRequest { BranchId = branch.Id, Items = [new() { ItemId = item.Id, Quantity = 1 }] });
        Check(scoped.Items.Single().UnitPrice > 0, "a seller outside every branch scope can still calculate prices");
    }
    var listedQuote = QuoteRequest(); listedQuote.PriceListId = fixList.Id;
    var listedOrder = await quotes.ConvertToOrderAsync((await quotes.CreateAsync(listedQuote)).Id);
    Check((await db.SalesOrders.AsNoTracking().SingleAsync(o => o.Id == listedOrder.SalesOrderId)).PriceListId == fixList.Id, "an order converted from a quotation keeps its price list");
    await Reject(async () => await lists.DeleteAsync(fixList.Id), "error.sd.pricelist_in_use");
    await Reject(async () => await orders.ConfirmAsync(approved.Id, default), "error.sd.order_invalid_status_for_action");
    await Reject(async () => await orders.UpdateAsync(approved.Id, new UpdateSalesOrderRequest { BranchId = branch.Id, Items = [new() { ItemId = rsv.Id, Quantity = 1, UnitPrice = 100 }] }), "error.sd.order_invalid_status_for_action");
    await Reject(async () => await orders.DeleteAsync(approved.Id), "error.sd.order_invalid_status_for_action");
    // Sales returns: stock back at the original cost, revenue and cost reversal, refund methods, limits.
    var revenueSwitch = await db.Accounts.SingleAsync(a => a.Code == "511"); revenueSwitch.IsActive = true; await db.SaveChangesAsync();   // earlier checks switched revenue off
    var returns = new SalesReturnService(db, new PostingTransaction(db));
    var ret = new Item { Code = "RET", Name = "Returnable goods", BaseUomId = uom.Id, SalePrice = 100, CostPrice = 1 };
    var retCustomer = new Partner { Code = "RETCUST", Name = "Return customer", CreditLimit = 100000, PartnerType = PartnerType.Customer };
    db.Items.Add(ret); db.Partners.Add(retCustomer); await db.SaveChangesAsync();
    db.StockBalances.Add(new StockBalance { WarehouseId = warehouse.Id, ItemId = ret.Id, QuantityOnHand = 10, AverageCostPrice = 6, InventoryValue = 60 }); await db.SaveChangesAsync();
    var soldInvoice = await Sales(db).CreateInvoiceAsync(new CreateSalesInvoiceRequest { RequestId = Guid.NewGuid(), WarehouseId = warehouse.Id, CustomerId = retCustomer.Id, PaidAmount = 100, Items = [new() { ItemId = ret.Id, Quantity = 4, UnitPrice = 100 }] });
    var soldLine = (await db.SalesInvoiceItems.AsNoTracking().SingleAsync(i => i.SalesInvoiceId == soldInvoice.Id)).Id;
    async Task<StockBalance> RetBalance() => await db.StockBalances.AsNoTracking().SingleAsync(b => b.ItemId == ret.Id);
    CreateSalesReturnRequest ReturnOf(decimal quantity, RefundMethod method, int? invoiceId = null, int? lineId = null, decimal price = 0) => new()
    {
        InvoiceId = invoiceId, CustomerId = invoiceId == null ? retCustomer.Id : null, BranchId = branch.Id, WarehouseId = warehouse.Id, RefundMethod = (int)method, Reason = "Faulty",
        Items = [new() { InvoiceItemId = lineId, ItemId = ret.Id, Quantity = quantity, UnitPrice = price, Reason = "Broken" }]
    };
    Check((await RetBalance()).QuantityOnHand == 6 && (await RetBalance()).InventoryValue == 36 && await new PostingTransaction(db).ReceivablesAsync(retCustomer.Id, default) == 300, "the sale leaves 6 in stock and 300 receivable");

    var creditRefund = await returns.CreateAsync(ReturnOf(1, RefundMethod.ReduceDebt, soldInvoice.Id, soldLine));
    Check(System.Text.RegularExpressions.Regex.IsMatch(creditRefund.ReturnNumber, @"^SR-\d{6}-\d{4,}$") && creditRefund.Status == "Posted" && creditRefund.TotalRefundAmount == 100 && creditRefund.InvoiceNumber == soldInvoice.InvoiceNumber
        && creditRefund.Items.Single().UnitPrice == 100 && creditRefund.StockReceiptId != null && creditRefund.JournalEntryId != null, "a return against an invoice is posted with its refund, receipt and journal");
    var retStock = await RetBalance();
    Check(retStock.QuantityOnHand == 7 && retStock.InventoryValue == 42 && retStock.AverageCostPrice == 6, "returned goods go back into stock at the cost they left it");
    var creditLines = await db.JournalEntryLines.AsNoTracking().Where(l => l.JournalEntryId == creditRefund.JournalEntryId).Select(l => new { l.Account!.Code, l.DebitAmount, l.CreditAmount }).ToListAsync();
    Check(creditLines.Count == 4 && creditLines.Single(l => l.Code == "521").DebitAmount == 100 && creditLines.Single(l => l.Code == "131").CreditAmount == 100
        && creditLines.Single(l => l.Code == "156").DebitAmount == 6 && creditLines.Single(l => l.Code == "632").CreditAmount == 6, "the return reverses revenue against receivables and cost of sales against inventory");
    Check(await new PostingTransaction(db).ReceivablesAsync(retCustomer.Id, default) == 200 && (await db.Partners.AsNoTracking().SingleAsync(p => p.Id == retCustomer.Id)).DebtBalance == 200, "a credit-memo return reduces the customer's receivable");

    await Reject(async () => await returns.CreateAsync(ReturnOf(4, RefundMethod.Cash, soldInvoice.Id, soldLine)), "error.sd.return_quantity_exceeds_invoiced");
    await Reject(async () => await returns.CreateAsync(ReturnOf(3, RefundMethod.ReduceDebt, soldInvoice.Id, soldLine)), "error.sd.return_refund_exceeds_debt");
    var cashRefund = await returns.CreateAsync(ReturnOf(3, RefundMethod.Cash, soldInvoice.Id, soldLine));
    Check(cashRefund.TotalRefundAmount == 300 && (await db.JournalEntryLines.AsNoTracking().AnyAsync(l => l.JournalEntryId == cashRefund.JournalEntryId && l.Account!.Code == "111" && l.CreditAmount == 300)), "a cash refund credits the cash account");
    await Reject(async () => await returns.CreateAsync(ReturnOf(1, RefundMethod.Cash, soldInvoice.Id, soldLine)), "error.sd.return_quantity_exceeds_invoiced");
    await Reject(async () => await Sales(db).CancelInvoiceAsync(soldInvoice.Id, "late"), "error.sd.invoice_has_returns");

    var freeRefund = await returns.CreateAsync(ReturnOf(2, RefundMethod.BankTransfer, price: 90));
    var freeStock = await RetBalance();
    Check(freeRefund.InvoiceId == null && freeRefund.TotalRefundAmount == 180 && freeStock.QuantityOnHand == 12 && freeStock.AverageCostPrice == 6 && await db.JournalEntryLines.AsNoTracking().AnyAsync(l => l.JournalEntryId == freeRefund.JournalEntryId && l.Account!.Code == "112" && l.CreditAmount == 180),
        "a return without invoice is priced as asked and costed at the average cost");
    var anonymousReturn = ReturnOf(1, RefundMethod.ReduceDebt, price: 90); anonymousReturn.CustomerId = null;
    await Reject(async () => await returns.CreateAsync(anonymousReturn), "error.sales.customer_required");
    var noReason = ReturnOf(1, RefundMethod.Cash, price: 90); noReason.Reason = " ";
    await Reject(async () => await returns.CreateAsync(noReason), "error.validation.request");
    await Reject(async () => await returns.UpdateAsync(freeRefund.Id, ReturnOf(1, RefundMethod.Cash, price: 90)), "error.accounting.posted_immutable");
    await Reject(async () => await returns.DeleteAsync(freeRefund.Id), "error.accounting.posted_immutable");

    var discounted = await Sales(db).CreateInvoiceAsync(new CreateSalesInvoiceRequest { RequestId = Guid.NewGuid(), WarehouseId = warehouse.Id, DiscountAmount = 20, PaidAmount = 180, Items = [new() { ItemId = ret.Id, Quantity = 2, UnitPrice = 100 }] });
    var discountedLine = (await db.SalesInvoiceItems.AsNoTracking().SingleAsync(i => i.SalesInvoiceId == discounted.Id)).Id;
    var shareRefund = await returns.CreateAsync(new CreateSalesReturnRequest { InvoiceId = discounted.Id, BranchId = branch.Id, WarehouseId = warehouse.Id, RefundMethod = (int)RefundMethod.Cash, Reason = "Changed mind", Items = [new() { InvoiceItemId = discountedLine, ItemId = ret.Id, Quantity = 1 }] });
    Check(shareRefund.TotalRefundAmount == 90 && shareRefund.CustomerName == discounted.CustomerName, "an invoice discount is shared across the returned quantity");
    var invoiceDetail = await Sales(db).GetInvoiceAsync(soldInvoice.Id);
    Check(invoiceDetail != null && invoiceDetail.Items.Single().Id == soldLine && await Sales(db).GetInvoiceAsync(999999) == null, "an invoice is readable by id with its line ids for returns");
    var listed = await returns.GetPagedAsync(new BaseFilterQuery { Keyword = creditRefund.ReturnNumber, Status = "Posted" });
    Check(listed.TotalCount == 1 && listed.Items.Single().Items.Count == 0, "the return page filters by number and status and returns headers only");
    Check(PermissionCatalog.Contains("sd:return:view") && PermissionCatalog.Contains("sd:return:create"), "return permissions are in the catalog");
    (await db.Accounts.SingleAsync(a => a.Code == "511")).IsActive = false; await db.SaveChangesAsync();   // restore the state the next checks expect
    // Sales analytics (Dapper SQL) cross-checked against EF queries over the same data.
    var analytics = new SalesAnalyticsService(db, new BranchService(db));
    var allTime = new SalesAnalyticsQuery { From = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc), To = DateTime.UtcNow.AddDays(1) };
    var sellerPosition = await db.JobPositions.FirstOrDefaultAsync() ?? db.JobPositions.Add(new JobPosition { Code = "SELLER", Name = "Seller", CompanyId = branch.Id }).Entity;
    await db.SaveChangesAsync();
    var seller = await db.Employees.FirstOrDefaultAsync() ?? db.Employees.Add(new Employee { Code = "SELLER", FullName = "Seller", CompanyId = branch.Id, BranchId = branch.Id, JobPositionId = sellerPosition.Id }).Entity;
    await db.SaveChangesAsync();
    await db.SalesOrders.Where(o => o.Id == approved.Id).ExecuteUpdateAsync(o => o.SetProperty(x => x.SalespersonId, seller.Id));
    var summary = await analytics.GetSummaryAsync(allTime);
    var invoiced = await db.SalesInvoices.Where(i => i.Status == VoucherStatus.Posted).SumAsync(i => i.TotalAmount);
    var invoicedCost = await db.SalesInvoiceItems.Where(i => i.SalesInvoice!.Status == VoucherStatus.Posted).SumAsync(i => i.CostAmount);
    var refunded = await db.SalesReturns.Where(r => r.Status == VoucherStatus.Posted).SumAsync(r => r.TotalRefundAmount);
    var restocked = await db.SalesReturns.Where(r => r.Status == VoucherStatus.Posted).SumAsync(r => r.TotalCostAmount);
    Check(summary.TotalRevenue == invoiced - refunded && summary.TotalCost == invoicedCost - restocked && summary.GrossProfit == summary.TotalRevenue - summary.TotalCost && refunded > 0 && summary.TotalRevenue > 0,
        "the sales summary nets posted returns off revenue and cost");
    Check(summary.GrossMarginPercent == Math.Round(summary.GrossProfit / summary.TotalRevenue * 100m, 2), "the gross margin is profit over revenue");
    Check(summary.TotalOrders == await db.SalesOrders.CountAsync() && summary.CancelledOrders == await db.SalesOrders.CountAsync(o => o.Status == "Cancelled") && summary.CancelledOrders > 0 && summary.CompletedOrders == 0, "the sales summary counts orders by status");
    var funnel = await analytics.GetFunnelAsync(allTime);
    var liveOrders = db.SalesOrders.Where(o => o.Status == "Confirmed" || o.Status == "Processing" || o.Status == "Completed");
    var quotationCount = await db.SalesQuotations.CountAsync();
    Check(funnel.QuotationCount == quotationCount && funnel.QuotationTotalAmount == await db.SalesQuotations.SumAsync(q => q.TotalAmount) && funnel.OrderCount == await liveOrders.CountAsync() && funnel.OrderTotalAmount == await liveOrders.SumAsync(o => o.TotalAmount)
        && funnel.InvoiceCount == await db.SalesInvoices.CountAsync(i => i.Status == VoucherStatus.Posted) && funnel.InvoiceTotalAmount == invoiced, "the sales funnel counts quotations, live orders and posted invoices");
    Check(funnel.ConversionRatePercent == Math.Round(await db.SalesQuotations.CountAsync(q => q.Status == "Converted") * 100m / quotationCount, 2) && funnel.ConversionRatePercent > 0, "the funnel reports the quotation conversion rate");
    var kpi = await analytics.GetSalespersonKpiAsync(allTime);
    var sellerOrders = liveOrders.Where(o => o.SalespersonId == seller.Id);
    Check(kpi.Count == 1 && kpi[0].SalespersonId == seller.Id && kpi[0].SalespersonName == seller.FullName && kpi[0].TotalOrders == await sellerOrders.CountAsync() && kpi[0].TotalRevenue == await sellerOrders.SumAsync(o => o.TotalAmount) && kpi[0].TargetRevenue == null,
        "the salesperson KPI totals the live orders of each seller");
    var thisBranch = await analytics.GetSummaryAsync(new SalesAnalyticsQuery { From = allTime.From, To = allTime.To, BranchId = branch.Id });
    Check(thisBranch.TotalRevenue == summary.TotalRevenue && thisBranch.TotalOrders == summary.TotalOrders, "the branch filter keeps the figures of that branch");
    var future = await analytics.GetSummaryAsync(new SalesAnalyticsQuery { From = DateTime.UtcNow.AddDays(30), To = DateTime.UtcNow.AddDays(60) });
    Check(future.TotalRevenue == 0 && future.TotalOrders == 0 && (await analytics.GetSalespersonKpiAsync(new SalesAnalyticsQuery { From = DateTime.UtcNow.AddDays(30), To = DateTime.UtcNow.AddDays(60) })).Count == 0, "a period without activity reports zero");
    await Reject(async () => await analytics.GetSummaryAsync(new SalesAnalyticsQuery { From = allTime.To, To = allTime.From }), "error.validation.request");
    await using (var elsewhere = Db(new TestUser(false, [999999])))
    {
        var hidden = new SalesAnalyticsService(elsewhere, new BranchService(elsewhere));
        var none = await hidden.GetSummaryAsync(allTime);
        var noFunnel = await hidden.GetFunnelAsync(allTime);
        Check(none.TotalRevenue == 0 && none.TotalOrders == 0 && noFunnel.OrderCount == 0 && noFunnel.QuotationCount == 0 && noFunnel.InvoiceCount == 0 && (await hidden.GetSalespersonKpiAsync(allTime)).Count == 0, "analytics only count records inside the caller's data scope");
    }
    Check(PermissionCatalog.Contains("sd:report:view"), "the report permission is in the catalog");
    // F5: a SELF-scope seller can raise their own quotation and order although no branch is in their scope.
    await using (var selfDb = Db(new SelfUser()))
    {
        var selfQuote = await new SalesQuotationService(selfDb, new PostingTransaction(selfDb)).CreateAsync(QuoteRequest());
        var selfOrder = await new SalesOrderService(selfDb, new PostingTransaction(selfDb)).CreateAsync(new CreateSalesOrderRequest { BranchId = branch.Id, CustomerId = rich.Id, Items = [new() { ItemId = rsv.Id, Quantity = 1, UnitPrice = 100 }] });
        Check(selfQuote.Id > 0 && selfOrder.Id > 0 && selfOrder.BranchName == branch.Name && selfQuote.BranchName == branch.Name, "a seller limited to their own records can create a quotation and an order");
        Check(await db.SalesOrders.AsNoTracking().AnyAsync(o => o.Id == selfOrder.Id && o.CreatedBy == "seller"), "the seller's order is stored with its author");
        var mine = await new SalesOrderService(selfDb, new PostingTransaction(selfDb)).GetPagedAsync(new BaseFilterQuery());
        var myQuotes = await new SalesQuotationService(selfDb, new PostingTransaction(selfDb)).GetPagedAsync(new BaseFilterQuery());
        Check(mine.TotalCount == 1 && mine.Items.Single().Id == selfOrder.Id && myQuotes.TotalCount == 1 && myQuotes.Items.Single().Id == selfQuote.Id, "a seller limited to their own records lists exactly their own orders and quotations");
    }
    // create-invoice: an order is invoiced once at its agreed prices, its held stock is consumed.
    (await db.Accounts.SingleAsync(a => a.Code == "511")).IsActive = true; await db.SaveChangesAsync();   // earlier checks switched revenue off
    var inv = new Item { Code = "INV", Name = "Invoiced goods", BaseUomId = uom.Id, SalePrice = 100, CostPrice = 1 };
    db.Items.Add(inv); await db.SaveChangesAsync();
    db.StockBalances.Add(new StockBalance { WarehouseId = warehouse.Id, ItemId = inv.Id, QuantityOnHand = 10, AverageCostPrice = 5, InventoryValue = 50 }); await db.SaveChangesAsync();
    async Task<StockBalance> InvStock() => await db.StockBalances.AsNoTracking().SingleAsync(b => b.ItemId == inv.Id);
    var seller3 = new SalesInvoiceService(db, new BranchService(db), new PostingTransaction(db), new TestUser(false, [branch.Id]));
    async Task<SalesOrderDto> InvOrder(int? customerId, decimal quantity, decimal price, decimal lineDiscount = 0, decimal discount = 0, decimal tax = 0) => await orders.CreateAsync(new CreateSalesOrderRequest
    { BranchId = branch.Id, CustomerId = customerId, DiscountAmount = discount, Items = [new() { ItemId = inv.Id, Quantity = quantity, UnitPrice = price, DiscountAmount = lineDiscount, VatRate = tax }] });
    CreateInvoiceFromOrderRequest FromOrder(PaymentMethod method = PaymentMethod.Cash, decimal tendered = 0) => new() { WarehouseId = warehouse.Id, PaymentMethod = (int)method, TenderedAmount = tendered };

    var pos150 = new CreateSalesInvoiceRequest { RequestId = Guid.NewGuid(), WarehouseId = warehouse.Id, PaidAmount = 150, Items = [new() { ItemId = inv.Id, Quantity = 1, UnitPrice = 150 }] };
    await Reject(async () => await seller3.CreateInvoiceAsync(pos150), "error.sales.price_override");

    var agreed = await orders.ConfirmAsync((await InvOrder(rich.Id, 3, 150, lineDiscount: 30, discount: 20)).Id, default);
    Check(agreed.TotalAmount == 400 && agreed.ReservationStatus == "Reserved" && (await InvStock()).QuantityReserved == 3, "an order priced above the catalogue is confirmed and its stock held");
    await Reject(async () => await seller3.CreateInvoiceFromOrderAsync(999999, FromOrder()), "error.order.not_found");
    var raised = await seller3.CreateInvoiceFromOrderAsync(agreed.Id, FromOrder(PaymentMethod.Cash, 400));
    Check(raised.SalesOrderId == agreed.Id && raised.TotalAmount == 400 && raised.PaidAmount == 400 && raised.RemainingDebt == 0 && raised.Items.Single().UnitPrice == 150 && raised.Items.Single().Quantity == 3,
        "an invoice from an order keeps the agreed price, discounts and total");
    var consumed = await InvStock();
    Check(consumed.QuantityOnHand == 7 && consumed.QuantityReserved == 0 && consumed.InventoryValue == 35, "the invoice issues the stock the order held");
    var invoicedOrder = await orders.GetByIdAsync(agreed.Id);
    Check(invoicedOrder!.Status == "Completed" && invoicedOrder.Items.Single().InvoicedQuantity == 3 && invoicedOrder.ReservationStatus == "Released"
        && !await db.StockReservations.AsNoTracking().AnyAsync(r => r.SalesOrderId == agreed.Id && r.Status == "Active"), "the invoiced order is Completed with its reservation released");
    Check((await Sales(db).GetInvoiceAsync(raised.Id))!.SalesOrderId == agreed.Id, "the invoice remembers its order");
    await Reject(async () => await seller3.CreateInvoiceFromOrderAsync(agreed.Id, FromOrder(PaymentMethod.Cash, 400)), "error.sd.order_invalid_status_for_action");
    await Reject(async () => await Sales(db).CancelInvoiceAsync(raised.Id, "oops"), "error.sd.invoice_from_order_locked");
    await Reject(async () => await orders.CancelAsync(agreed.Id, "late", default), "error.sd.cannot_cancel_invoiced_order");
    Check(await db.JournalEntryLines.AsNoTracking().Where(l => l.JournalEntryId == db.SalesInvoices.Where(i => i.Id == raised.Id).Select(i => i.JournalEntryId).FirstOrDefault()).SumAsync(l => l.DebitAmount)
        == await db.JournalEntryLines.AsNoTracking().Where(l => l.JournalEntryId == db.SalesInvoices.Where(i => i.Id == raised.Id).Select(i => i.JournalEntryId).FirstOrDefault()).SumAsync(l => l.CreditAmount), "the order invoice journal balances");

    var draftOnly = await InvOrder(rich.Id, 1, 100);
    await Reject(async () => await seller3.CreateInvoiceFromOrderAsync(draftOnly.Id, FromOrder()), "error.sd.order_invalid_status_for_action");
    var taxed = await orders.ConfirmAsync((await InvOrder(rich.Id, 1, 100, tax: 10)).Id, default);
    await Reject(async () => await seller3.CreateInvoiceFromOrderAsync(taxed.Id, FromOrder()), "error.sd.order_tax_not_supported");
    // An approved credit is honoured when the order is invoiced; an order that was never approved is still held to the limit.
    await db.StockBalances.Where(b => b.ItemId == inv.Id).ExecuteUpdateAsync(b => b.SetProperty(x => x.QuantityOnHand, 100m));
    var approvedPartner = new Partner { Code = "APPR", Name = "Approved", CreditLimit = 0, PartnerType = PartnerType.Customer };
    var heldPartner = new Partner { Code = "LIM2", Name = "Limited 2", CreditLimit = 1000, PartnerType = PartnerType.Customer };
    db.Partners.AddRange(approvedPartner, heldPartner); await db.SaveChangesAsync();
    var waitingOrder = await orders.ConfirmAsync((await InvOrder(approvedPartner.Id, 1, 100)).Id, default);
    Check(waitingOrder.Status == "PendingApproval", "an order over the credit limit waits for approval before it can be invoiced");
    await Reject(async () => await seller3.CreateInvoiceFromOrderAsync(waitingOrder.Id, FromOrder(PaymentMethod.Debt)), "error.sd.order_invalid_status_for_action");
    await new SalesOrderService(db, new PostingTransaction(db), null, null, user).ApproveCreditAsync(waitingOrder.Id, "Prepaid by wire", default);
    var approvalOnOrder = await db.SalesOrders.AsNoTracking().SingleAsync(o => o.Id == waitingOrder.Id);
    Check(approvalOnOrder.CreditApprovedBy == "verification" && approvalOnOrder.CreditApprovedAtUtc != null && approvalOnOrder.CreditApprovalReason == "Prepaid by wire", "the approver, the time and the reason are kept on the order");
    var onCredit = await seller3.CreateInvoiceFromOrderAsync(waitingOrder.Id, FromOrder(PaymentMethod.Debt));
    Check(onCredit.RemainingDebt == 100 && (await db.Partners.AsNoTracking().SingleAsync(p => p.Id == approvedPartner.Id)).DebtBalance == 100, "an order whose credit was approved is invoiced on credit above the limit and its debt is booked");
    var withinLimit = await orders.ConfirmAsync((await InvOrder(heldPartner.Id, 1, 600)).Id, default);
    Check(withinLimit.CreditCheckStatus == "Passed" && withinLimit.Status == "Confirmed", "an order inside the limit is confirmed without approval");
    await Sales(db).CreateInvoiceAsync(new CreateSalesInvoiceRequest { RequestId = Guid.NewGuid(), WarehouseId = warehouse.Id, CustomerId = heldPartner.Id, PaidAmount = 0, Items = [new() { ItemId = inv.Id, Quantity = 1, UnitPrice = 100 }] });
    await db.Partners.Where(p => p.Id == heldPartner.Id).ExecuteUpdateAsync(p => p.SetProperty(x => x.CreditLimit, 650m));
    await Reject(async () => await seller3.CreateInvoiceFromOrderAsync(withinLimit.Id, FromOrder(PaymentMethod.Debt)), "error.sales.credit_limit");
    var retail = await orders.ConfirmAsync((await InvOrder(null, 2, 100)).Id, default);
    var heldBefore = (await InvStock()).QuantityReserved;
    await Reject(async () => await seller3.CreateInvoiceFromOrderAsync(retail.Id, FromOrder(PaymentMethod.Debt)), "error.sales.customer_required");
    Check((await InvStock()).QuantityReserved == heldBefore && (await orders.GetByIdAsync(retail.Id))!.Status == "Confirmed", "a refused invoice leaves the order Confirmed with its stock still held");
    (await db.Accounts.SingleAsync(a => a.Code == "511")).IsActive = false; await db.SaveChangesAsync();   // restore the state the next checks expect
    // Quotation notifications follow the customer's answer and the conversion, and only for changes that were saved.
    // Who hears about it: accounts that may view quotations within a scope reaching this one, never the person acting.
    var quoteActor = new AppUser { UserName = "QN_ACTOR", DisplayName = "Actor", Role = "Viewer" };
    var quoteViewer = new AppUser { UserName = "QN_VIEWER", DisplayName = "Viewer in branch", Role = "Viewer" };
    var quoteBoss = new AppUser { UserName = "QN_BOSS", DisplayName = "Unrestricted viewer", Role = "Viewer" };
    var quoteOwner = new AppUser { UserName = "QN_OWNER", DisplayName = "Seller of the quotation", Role = "Viewer" };
    var quoteStranger = new AppUser { UserName = "QN_STRANGER", DisplayName = "Seller of another record", Role = "Viewer" };
    var quoteOtherBranch = new AppUser { UserName = "QN_OTHER", DisplayName = "Viewer of another branch", Role = "Viewer" };
    var quoteNoRight = new AppUser { UserName = "QN_NORIGHT", DisplayName = "No quotation right", Role = "Viewer" };
    db.AppUsers.AddRange(quoteActor, quoteViewer, quoteBoss, quoteOwner, quoteStranger, quoteOtherBranch, quoteNoRight); await db.SaveChangesAsync();
    IamPrincipal QuotePrincipal(string action, IamDataScope scope, string userKey = "nobody") => new([SystemRoles.Allow("Quote", [action], scope)], new OrgContext { CompanyId = 1, UserKey = userKey, Branches = new OrgTree([(branch.Id, (int?)null), (999999, (int?)null)]) }, false);
    var inBranch = new IamDataScope { Mode = DataScopeModes.CustomBranches, CustomBranchIds = [branch.Id] };
    var quoteIam = new IamStub(new()
    {
        [quoteActor.Id] = QuotePrincipal("sd:quotation:view", inBranch),
        [quoteViewer.Id] = QuotePrincipal("sd:quotation:view", inBranch),
        [quoteBoss.Id] = QuotePrincipal("sd:quotation:view", new IamDataScope { Mode = DataScopeModes.Group }),
        [quoteOwner.Id] = QuotePrincipal("sd:quotation:view", new IamDataScope { Mode = DataScopeModes.Self }, userKey: "verification"),
        [quoteStranger.Id] = QuotePrincipal("sd:quotation:view", new IamDataScope { Mode = DataScopeModes.Self }, userKey: "someone-else"),
        [quoteOtherBranch.Id] = QuotePrincipal("sd:quotation:view", new IamDataScope { Mode = DataScopeModes.CustomBranches, CustomBranchIds = [999999] }),
        [quoteNoRight.Id] = QuotePrincipal("sd:order:view", inBranch),
    });
    var recorder = new RecordingEngine();
    var talking = new SalesQuotationService(db, new PostingTransaction(db), recorder, quoteIam, new ActorUser(quoteActor.Id));
    int?[] Told(string template) => recorder.Events.OfType<ERPm.Domain.Events.NotificationEvent>().Where(e => e.TemplateCode == template).Select(e => e.TargetId).OrderBy(id => id).ToArray();
    int?[] Expected() => new int?[] { quoteViewer.Id, quoteBoss.Id, quoteOwner.Id }.OrderBy(id => id).ToArray();
    var announced = await talking.CreateAsync(QuoteRequest());
    await talking.SendAsync(announced.Id);
    Check(recorder.Events.Count == 0, "sending a quotation notifies nobody");
    await talking.AcceptAsync(announced.Id);
    var acceptedEvents = recorder.Events.OfType<ERPm.Domain.Events.NotificationEvent>().ToList();
    Check(Told("SD_QUOTATION_ACCEPTED").SequenceEqual(Expected()) && acceptedEvents.All(e => e.TargetType == "USER" && e.BranchId == branch.Id && e.Parameters["quotation_code"].ToString() == announced.QuotationNumber),
        "an accepted quotation notifies only accounts that may view it in a scope reaching it, not the actor, other branches or people without the right");
    var conversion = await talking.ConvertToOrderAsync(announced.Id);
    Check(Told("SD_QUOTATION_CONVERTED").SequenceEqual(Expected()) && recorder.Events.OfType<ERPm.Domain.Events.NotificationEvent>().First(e => e.TemplateCode == "SD_QUOTATION_CONVERTED").Parameters["order_code"].ToString() == conversion.OrderNumber, "a converted quotation tells the same people with the new order number");
    await Reject(async () => await talking.ConvertToOrderAsync(announced.Id), "error.sd.quotation_already_converted");
    var turnedDown = await talking.CreateAsync(QuoteRequest());
    await talking.SendAsync(turnedDown.Id);
    await talking.RejectAsync(turnedDown.Id);
    Check(Told("SD_QUOTATION_REJECTED").SequenceEqual(Expected()) && recorder.Events.OfType<ERPm.Domain.Events.NotificationEvent>().First(e => e.TemplateCode == "SD_QUOTATION_REJECTED").Severity == "WARNING", "a rejected quotation raises a warning for the same people");
    Check(!Told("SD_QUOTATION_ACCEPTED").Contains(quoteActor.Id) && !Told("SD_QUOTATION_REJECTED").Contains(quoteNoRight.Id), "the actor is left out of the recipients of their own change");

    // Order and return notices reach the same kind of people: viewers whose scope reaches the document, never the actor.
    IamPrincipal DocPrincipal(IamDataScope scope, string userKey, params string[] actions) => new([SystemRoles.Allow("Doc", actions, scope)], new OrgContext { CompanyId = 1, UserKey = userKey, Branches = new OrgTree([(branch.Id, (int?)null), (999999, (int?)null)]) }, false);
    string[] docRights = ["sd:order:view", "sd:return:view", "sd:invoice:view", "wms:stock:view"];
    string[] approverRights = [.. docRights, "sd:order:approve_credit"];
    var docIam = new IamStub(new()
    {
        [quoteActor.Id] = DocPrincipal(inBranch, "nobody", docRights),
        [quoteViewer.Id] = DocPrincipal(inBranch, "nobody", approverRights),
        [quoteBoss.Id] = DocPrincipal(new IamDataScope { Mode = DataScopeModes.Group }, "nobody", docRights),
        [quoteOwner.Id] = DocPrincipal(new IamDataScope { Mode = DataScopeModes.Self }, "verification", docRights),
        [quoteStranger.Id] = DocPrincipal(new IamDataScope { Mode = DataScopeModes.Self }, "someone-else", docRights),
        [quoteOtherBranch.Id] = DocPrincipal(new IamDataScope { Mode = DataScopeModes.CustomBranches, CustomBranchIds = [999999] }, "nobody", docRights),
        [quoteNoRight.Id] = DocPrincipal(inBranch, "nobody", "sd:quotation:view"),
    });
    var docEngine = new RecordingEngine();
    int?[] DocTold(string template) => docEngine.Events.OfType<ERPm.Domain.Events.NotificationEvent>().Where(e => e.TemplateCode == template).Select(e => e.TargetId).OrderBy(id => id).ToArray();
    var docOrders = new SalesOrderService(db, new PostingTransaction(db), docEngine, docIam, new ActorUser(quoteActor.Id));
    var noticeOrder = await docOrders.CreateAsync(new CreateSalesOrderRequest { BranchId = branch.Id, CustomerId = rich.Id, Items = [new() { ItemId = creditItem.Id, Quantity = 1, UnitPrice = 100 }] });
    Check(docEngine.Events.Count == 0, "saving a draft order notifies nobody");
    await docOrders.ConfirmAsync(noticeOrder.Id, default);
    Check(DocTold("SD_ORDER_NEW").SequenceEqual(Expected()) && docEngine.Events.OfType<ERPm.Domain.Events.NotificationEvent>().All(e => e.TargetType == "USER" && e.BranchId == branch.Id),
        "a confirmed order notifies viewers in a scope reaching it (branch, unrestricted, the seller who owns it), not the actor, other branches or people without the right");
    await docOrders.CancelAsync(noticeOrder.Id, "notice test", default);
    Check(DocTold("SD_ORDER_CANCELLED").SequenceEqual(Expected()), "a cancelled order tells the same people");
    var heldNotice = await docOrders.ConfirmAsync((await docOrders.CreateAsync(new CreateSalesOrderRequest { BranchId = branch.Id, CustomerId = poor.Id, Items = [new() { ItemId = creditItem.Id, Quantity = 1, UnitPrice = 100 }] })).Id, default);
    Check(heldNotice.Status == "PendingApproval" && DocTold("SD_ORDER_CREDIT_PENDING").SequenceEqual(new int?[] { quoteViewer.Id }), "an order waiting for credit approval tells only the accounts that may approve credit in a scope reaching it, not other viewers");
    await docOrders.ApproveCreditAsync(heldNotice.Id, "notice test", default);
    Check(DocTold("SD_ORDER_CREDIT_APPROVED").SequenceEqual(Expected()), "an approved credit tells the same people");
    var docReturns = new SalesReturnService(db, new PostingTransaction(db), docEngine, docIam, new ActorUser(quoteActor.Id));
    await docReturns.CreateAsync(ReturnOf(1, RefundMethod.Cash, price: 10));
    Check(DocTold("SD_RETURN_POSTED").SequenceEqual(Expected()), "a posted return notifies viewers of returns in a scope reaching it, not the actor");
    (await db.Accounts.SingleAsync(a => a.Code == "511")).IsActive = true; await db.SaveChangesAsync();   // earlier checks switched revenue off
    var invoicer = new SalesInvoiceService(db, new BranchService(db), new PostingTransaction(db), new ActorUser(quoteActor.Id), docEngine, docIam);
    var toInvoice = await docOrders.ConfirmAsync((await docOrders.CreateAsync(new CreateSalesOrderRequest { BranchId = branch.Id, CustomerId = rich.Id, Items = [new() { ItemId = creditItem.Id, Quantity = 1, UnitPrice = 100 }] })).Id, default);
    await invoicer.CreateInvoiceFromOrderAsync(toInvoice.Id, FromOrder(PaymentMethod.Cash, 100));
    // A sale that leaves stock at or under the item's alert level, then its cancellation, tell stock/invoice viewers only after the commit.
    await db.Items.Where(i => i.Id == item.Id).ExecuteUpdateAsync(s => s.SetProperty(i => i.MinStockAlert, 1_000_000_000m)); db.ChangeTracker.Clear();
    var alertSale = await invoicer.CreateInvoiceAsync(Sale());
    await db.Items.Where(i => i.Id == item.Id).ExecuteUpdateAsync(s => s.SetProperty(i => i.MinStockAlert, 0m));
    Check(DocTold("WMS_LOW_STOCK").SequenceEqual(new int?[] { quoteViewer.Id, quoteBoss.Id }.OrderBy(id => id)), "low stock tells accounts that may view stock in the warehouse branch (stock has no owner, so seller-only scopes get nothing), not the actor, other branches or people without the right");
    await invoicer.CancelInvoiceAsync(alertSale.Id, "notice test");
    Check(DocTold("SD_INVOICE_CANCELLED").SequenceEqual(Expected()), "a cancelled invoice tells viewers of invoices in a scope reaching it, not the actor");
    var eventsBefore = docEngine.Events.Count;
    await Reject(async () => await invoicer.CancelInvoiceAsync(alertSale.Id, " "), "error.validation.request");
    Check(docEngine.Events.Count == eventsBefore, "a refused action notifies nobody");
    (await db.Accounts.SingleAsync(a => a.Code == "511")).IsActive = false; await db.SaveChangesAsync();   // restore the state the next checks expect
    Check(DocTold("SD_ORDER_INVOICED").SequenceEqual(Expected()), "an order turned into an invoice tells viewers of the order, not the person who invoiced it");
    Check(docEngine.Events.OfType<ERPm.Domain.Events.NotificationEvent>().All(e => e.TargetId != quoteActor.Id && e.TargetId != quoteNoRight.Id && e.TargetId != quoteOtherBranch.Id && e.TargetId != quoteStranger.Id), "nobody outside the right scope or the actor received any order or return notice");
    await using var denied = Db(new TestUser(false, [999999]));
    Check(!await denied.SalesInvoices.AnyAsync() && !await denied.StockBalances.AnyAsync(), "server filters inaccessible branches");
    db.ChangeTracker.Clear(); var immutable = await db.SalesInvoices.FirstAsync(); immutable.TotalAmount = 1;
    await Reject(async () => await db.SaveChangesAsync(), "error.accounting.posted_immutable");
    db.ChangeTracker.Clear();
    var immutableLine = await db.SalesInvoiceItems.FirstAsync(); immutableLine.Quantity += 1;
    await Reject(async () => await db.SaveChangesAsync(), "error.accounting.posted_immutable");
    db.ChangeTracker.Clear();
    var allowedBranch = new Branch { Code = "ALLOWED", Name = "Allowed" };
    var revenueAccount = await db.Accounts.SingleAsync(a => a.Code == "511"); revenueAccount.IsActive = true;
    var roundingItem = new Item { Code = "ROUNDING", Name = "Rounding", Translations = new() { ["en"] = "Translated product" }, BaseUomId = uom.Id, SalePrice = 100 };
    db.Items.Add(roundingItem); await db.SaveChangesAsync();
    var catalogue = new ItemService(db, new(db));
    var missingSaleId = Sale(); missingSaleId.RequestId = Guid.Empty;
    await Reject(async () => await Sales(db).CreateInvoiceAsync(missingSaleId), "error.validation.request");
    var missingReceiptId = Receipt(1, 1); missingReceiptId.RequestId = Guid.Empty;
    await Reject(async () => await Receipts(db).CreateAsync(missingReceiptId, default), "error.validation.request");
    var translatedSearch = await catalogue.GetItemsAsync(new ERPm.Application.DTOs.Catalog.ItemFilterQuery { Keyword = "translated" });
    Check(translatedSearch.Items.Any(i => i.Id == roundingItem.Id), "catalogue search includes translated names");
    var roundingReceipt1 = Receipt(3, 1); roundingReceipt1.Items[0].ItemId = roundingItem.Id;
    var roundingReceipt2 = Receipt(4, 2); roundingReceipt2.Items[0].ItemId = roundingItem.Id;
    await Receipts(db).CreateAsync(roundingReceipt1, default); await Receipts(db).CreateAsync(roundingReceipt2, default);
    var roundingBalance = await db.StockBalances.SingleAsync(b => b.ItemId == roundingItem.Id);
    Check(roundingBalance.InventoryValue == 11, "inventory carrying value does not drift when average cost is rounded");
    var roundingSale = Sale(7, 700); roundingSale.Items[0].ItemId = roundingItem.Id;
    var roundingInvoice = await Sales(db).CreateInvoiceAsync(roundingSale);
    db.ChangeTracker.Clear();
    roundingBalance = await db.StockBalances.SingleAsync(b => b.ItemId == roundingItem.Id);
    Check(roundingBalance.QuantityOnHand == 0 && roundingBalance.InventoryValue == 0, "final stock issue allocates the remaining carrying value");
    Check((await db.SalesInvoiceItems.SingleAsync(i => i.SalesInvoiceId == roundingInvoice.Id)).CostAmount == 11, "cost of sales equals the exact depleted inventory value");
    await Reject(async () => await catalogue.DeleteItemAsync(roundingItem.Id), "error.item.in_use");
    await Reject(async () => await catalogue.UpdateItemAsync(roundingItem.Id, new ERPm.Application.DTOs.Catalog.UpdateItemRequest { Code = "ROUNDING", Name = "Rounding", BaseUomId = uom.Id, ItemType = ItemType.Service, SalePrice = 100, IsActive = true }), "error.item.in_use");
    var deniedBranch = new Branch { Code = "DENIED", Name = "Denied" };
    db.Branches.AddRange(allowedBranch, deniedBranch); await db.SaveChangesAsync();
    IamChecks.Pure(Check);
    await IamChecks.DatabaseAsync(new(u => Db(u), Check, Reject));
    ERPm.Verification.NotificationChecks.Pure(Check);
    await ERPm.Verification.NotificationChecks.RunDbChecksAsync(new(u => Db(u), Check, Reject));
    using var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
    var password = "Verify-" + Guid.NewGuid().ToString("N");
    var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = Path.Combine(repo, "src/ERPm.API") };
    // Use the SDK host explicitly; the verification executable itself is not the API host.
    start.FileName = Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "../../../dotnet.exe");
    if (!OperatingSystem.IsWindows()) start.FileName = "dotnet";
    start.ArgumentList.Add(Path.Combine(repo, "src/ERPm.API/bin/Debug/net10.0/ERPm.API.dll"));
    start.ArgumentList.Add("--urls"); start.ArgumentList.Add($"http://127.0.0.1:{port}");
    start.Environment["ConnectionStrings__DefaultConnection"] = source.ConnectionString;
    start.Environment["BootstrapAdmin__Password"] = password;
    start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
    using var api = Process.Start(start)!;
    var stdout = api.StandardOutput.ReadToEndAsync(); var stderr = api.StandardError.ReadToEndAsync();
    try
    {
        using var http = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}"), Timeout = TimeSpan.FromSeconds(20) };
        for (var attempt = 0; ; attempt++)
        {
            try { using var response = await http.GetAsync("/api/v1/items"); Check(response.StatusCode == HttpStatusCode.Unauthorized, "anonymous API access is denied"); break; }
            catch (HttpRequestException) when (attempt < 60 && !api.HasExited) { await Task.Delay(200); }
        }
        async Task<string> Login(string name)
        {
            using var response = await http.PostAsJsonAsync("/api/v1/auth/login", new { userName = name, password });
            if (!response.IsSuccessStatusCode) throw new Exception("Login failed: " + await response.Content.ReadAsStringAsync());
            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return payload.RootElement.GetProperty("accessToken").GetString()!;
        }
        var adminToken = await Login("admin");
        http.DefaultRequestHeaders.Authorization = new("Bearer", adminToken);
        Check((await http.GetAsync("/api/v1/auth/me")).IsSuccessStatusCode, "internal account signs in with protected bearer token");
        using var created = await http.PostAsJsonAsync("/api/v1/auth/users", new { userName = "viewer", displayName = "Viewer", password, role = "Viewer", branchIds = new[] { allowedBranch.Id } });
        Check(created.IsSuccessStatusCode, "admin provisions scoped viewer");
        using var viewerPayload = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var viewerId = viewerPayload.RootElement.GetProperty("data").GetProperty("id").GetInt32();
        var viewerToken = await Login("viewer"); http.DefaultRequestHeaders.Authorization = new("Bearer", viewerToken);
        using var branchResponse = await http.GetAsync("/api/v1/branches");
        using var branchPayload = JsonDocument.Parse(await branchResponse.Content.ReadAsStringAsync());
        Check(branchPayload.RootElement.GetProperty("data").GetArrayLength() == 1, "HTTP branch scope excludes other branches");
        using var forbidden = await http.PostAsJsonAsync("/api/v1/stock/receipts", Receipt(1, 20));
        Check(forbidden.StatusCode == HttpStatusCode.Forbidden, "viewer cannot post stock receipts");
        using var branchEditDenied = await http.PutAsJsonAsync($"/api/v1/branches/{allowedBranch.Id}", new { code = "X", name = "X", parentId = (int?)null, isActive = true });
        using var branchDeleteDenied = await http.DeleteAsync($"/api/v1/branches/{allowedBranch.Id}");
        Check(branchEditDenied.StatusCode == HttpStatusCode.Forbidden && branchDeleteDenied.StatusCode == HttpStatusCode.Forbidden, "viewer cannot edit or delete branches");
        http.DefaultRequestHeaders.Authorization = new("Bearer", adminToken);
        using var order = await http.PostAsJsonAsync("/api/v1/sales/orders", new { branchId = branch.Id, customerId = customer.Id, discountAmount = 10, items = new[] { new { itemId = item.Id, quantity = 2, unitPrice = 100, vatRate = 2.5 } } });
        if (!order.IsSuccessStatusCode) throw new Exception("Order failed: " + await order.Content.ReadAsStringAsync());
        using var orderPayload = JsonDocument.Parse(await order.Content.ReadAsStringAsync());
        var orderData = orderPayload.RootElement.GetProperty("data"); var orderId = orderData.GetProperty("id").GetInt32();
        Check(orderData.GetProperty("totalAmount").GetDecimal() == 195 && orderData.GetProperty("items").GetArrayLength() == 1, "order totals are calculated by the server");
        using var confirmed = await http.PostAsync($"/api/v1/sales/orders/{orderId}/confirm", null);
        Check(confirmed.IsSuccessStatusCode, "order confirms without replacing its lines");
        using var confirmAgain = await http.PostAsync($"/api/v1/sales/orders/{orderId}/confirm", null);
        Check(confirmAgain.StatusCode == HttpStatusCode.Conflict, "confirmed order cannot be confirmed again");
        using var usersResponse = await http.GetAsync("/api/v1/auth/users");
        Check(usersResponse.IsSuccessStatusCode && !(await usersResponse.Content.ReadAsStringAsync()).Contains("passwordHash", StringComparison.OrdinalIgnoreCase), "user management never exposes password hashes");
        using var disabled = await http.PutAsJsonAsync($"/api/v1/auth/users/{viewerId}", new { displayName = "Disabled viewer", role = "Viewer", branchIds = new[] { allowedBranch.Id }, isActive = false });
        Check(disabled.IsSuccessStatusCode, "admin can revoke an account");
        using var usersAfterRevoke = JsonDocument.Parse(await http.GetStringAsync("/api/v1/auth/users"));
        Check(usersAfterRevoke.RootElement.GetProperty("data").EnumerateArray().Any(u => u.GetProperty("userName").GetString() == "VIEWER" && !u.GetProperty("isActive").GetBoolean()),
            "the user list reports a revoked account as inactive");
        http.DefaultRequestHeaders.Authorization = new("Bearer", viewerToken);
        Check((await http.GetAsync("/api/v1/auth/me")).StatusCode == HttpStatusCode.Unauthorized, "revoked accounts cannot reuse an existing token");

        // Step-20: permissions inherited from a job position, enforced over HTTP.
        http.DefaultRequestHeaders.Authorization = new("Bearer", adminToken);
        Check((await http.GetAsync("/api/v1/sys/permissions/catalog")).IsSuccessStatusCode && (await http.GetAsync("/api/v1/sys/roles")).IsSuccessStatusCode,
            "super administrator reads the permission catalog and roles");
        Check((await http.GetAsync("/api/v1/sys/notifications/unread-count")).IsSuccessStatusCode, "admin reads unread notification count over HTTP");
        Check((await http.GetAsync("/api/v1/sys/notifications")).IsSuccessStatusCode, "admin reads notification list over HTTP");
        // Step-22: price list endpoints, the fixed route "calculate-price" must not be taken for an id.
        using var listPage = JsonDocument.Parse(await http.GetStringAsync("/api/v1/sales/price-lists"));
        Check(listPage.RootElement.GetProperty("data").GetProperty("totalCount").GetInt32() >= 1, "admin reads the price list page over HTTP");
        using var priced = await http.PostAsJsonAsync("/api/v1/sales/price-lists/calculate-price", new { branchId = allowedBranch.Id, items = new[] { new { itemId = 1, quantity = 1 } } });
        Check(priced.StatusCode != HttpStatusCode.NotFound && priced.StatusCode != HttpStatusCode.MethodNotAllowed && priced.StatusCode != HttpStatusCode.Forbidden, "calculate-price is routed and allowed for the administrator");
        using var vipVersionsPayload = JsonDocument.Parse(await http.GetStringAsync($"/api/v1/sales/price-lists/{vip.Id}/versions"));
        var vipVersionsData = vipVersionsPayload.RootElement.GetProperty("data");
        var vipVersionId = vipVersionsData[0].GetProperty("id").GetInt32();
        Check(vipVersionsData.GetArrayLength() == 1 && vipVersionsData[0].GetProperty("versionCode").GetString() == "V1" && vipVersionsData[0].GetProperty("itemCount").GetInt32() == 1,
            "admin reads price list versions over HTTP");
        using var vipVersionDetailPayload = JsonDocument.Parse(await http.GetStringAsync($"/api/v1/sales/price-lists/{vip.Id}/versions/{vipVersionId}"));
        Check(vipVersionDetailPayload.RootElement.GetProperty("data").GetProperty("items").GetArrayLength() == 1, "admin reads a price list version with its items over HTTP");
        using var vipVersionItemsPayload = JsonDocument.Parse(await http.GetStringAsync($"/api/v1/sales/price-lists/{vip.Id}/versions/{vipVersionId}/items"));
        Check(vipVersionItemsPayload.RootElement.GetProperty("data").GetArrayLength() == 1, "admin reads a price list version's items endpoint over HTTP");
        Check((await http.GetAsync("/api/v1/sales/price-lists/999999/versions")).StatusCode == HttpStatusCode.NotFound
            && (await http.GetAsync($"/api/v1/sales/price-lists/{vip.Id}/versions/999999")).StatusCode == HttpStatusCode.NotFound,
            "reading versions of a missing price list or version answers not found");
        using var missingClone = await http.PostAsJsonAsync("/api/v1/sales/price-lists/1/versions/clone", new { sourceVersionId = 999999, versionCode = "API-CLONE", effectiveFrom = DateTime.UtcNow });
        Check(missingClone.StatusCode == HttpStatusCode.NotFound, "clone-version API route validates the source version");
        using var missingAdjustment = await http.PostAsJsonAsync("/api/v1/sales/price-lists/1/versions/999999/bulk-adjustment", new { adjustmentType = 1, value = 1, roundingIncrement = 0.01m });
        Check(missingAdjustment.StatusCode == HttpStatusCode.NotFound, "bulk-adjustment API route validates the target version");

        // S25.2b: HTTP routing, type filter, version lifecycle, Draft item CRUD and i18n error codes.
        using var createdPurchasePl = await http.PostAsJsonAsync("/api/v1/sales/price-lists", new
        {
            code = "PUR-HTTP",
            name = "Purchase HTTP",
            currency = "VND",
            type = 2,
            isTaxInclusive = false,
            effectiveFrom = DateTime.UtcNow.AddDays(-1),
            isActive = true
        });
        Check(createdPurchasePl.IsSuccessStatusCode, "admin creates a Purchase price list over HTTP");
        using var purchasePlPayload = JsonDocument.Parse(await createdPurchasePl.Content.ReadAsStringAsync());
        var purchasePlId = purchasePlPayload.RootElement.GetProperty("data").GetProperty("id").GetInt32();
        using var byTypeNamePurchase = JsonDocument.Parse(await http.GetStringAsync("/api/v1/sales/price-lists?type=Purchase&pageSize=50"));
        using var byTypeIntPurchase = JsonDocument.Parse(await http.GetStringAsync("/api/v1/sales/price-lists?type=2&pageSize=50"));
        using var byTypeNameSales = JsonDocument.Parse(await http.GetStringAsync("/api/v1/sales/price-lists?type=Sales&pageSize=50"));
        using var byTypeIntSales = JsonDocument.Parse(await http.GetStringAsync("/api/v1/sales/price-lists?type=1&pageSize=50"));
        Check(byTypeNamePurchase.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().Any(x => x.GetProperty("id").GetInt32() == purchasePlId)
            && byTypeIntPurchase.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().Any(x => x.GetProperty("id").GetInt32() == purchasePlId)
            && byTypeNameSales.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().All(x => x.GetProperty("id").GetInt32() != purchasePlId)
            && byTypeIntSales.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().All(x => x.GetProperty("id").GetInt32() != purchasePlId),
            "price-lists ?type=Sales|Purchase and 1|2 filter server-side over HTTP");
        Check((await http.GetAsync("/api/v1/sales/price-lists?type=99")).StatusCode == HttpStatusCode.BadRequest
            && (await http.GetAsync("/api/v1/sales/price-lists?type=InvalidType")).StatusCode == HttpStatusCode.BadRequest,
            "invalid price-lists ?type query answers bad request");

        var vStart = DateTime.UtcNow.AddDays(1);
        using var httpVersionResp = await http.PostAsJsonAsync($"/api/v1/sales/price-lists/{purchasePlId}/versions", new { versionCode = "HTTP-V2", effectiveFrom = vStart });
        Check(httpVersionResp.StatusCode == HttpStatusCode.Created, "POST versions creates a Draft version over HTTP");
        using var httpVersionPayload = JsonDocument.Parse(await httpVersionResp.Content.ReadAsStringAsync());
        var httpVersionId = httpVersionPayload.RootElement.GetProperty("data").GetProperty("id").GetInt32();
        Check(httpVersionPayload.RootElement.GetProperty("data").GetProperty("status").GetInt32() == 1, "HTTP created version starts in Draft status");

        using var httpVersionEdit = await http.PutAsJsonAsync($"/api/v1/sales/price-lists/{purchasePlId}/versions/{httpVersionId}", new { versionCode = "HTTP-V2-EDITED", effectiveFrom = vStart, effectiveTo = vStart.AddDays(15) });
        Check(httpVersionEdit.IsSuccessStatusCode, "PUT versions updates a Draft version over HTTP");

        using var httpAddItem = await http.PostAsJsonAsync($"/api/v1/sales/price-lists/{purchasePlId}/versions/{httpVersionId}/items", new { itemId = 1, unitPrice = 88, minQuantity = 1, discountPercent = 0 });
        Check(httpAddItem.StatusCode == HttpStatusCode.Created, "POST version items adds an item to a Draft version over HTTP");
        using var httpAddItemPayload = JsonDocument.Parse(await httpAddItem.Content.ReadAsStringAsync());
        var httpItemLineId = httpAddItemPayload.RootElement.GetProperty("data").GetProperty("id").GetInt32();

        using var httpAddItem2 = await http.PostAsJsonAsync($"/api/v1/sales/price-lists/{purchasePlId}/versions/{httpVersionId}/items", new { itemId = 1, unitPrice = 77, minQuantity = 10, discountPercent = 0 });
        using var httpAddItem2Payload = JsonDocument.Parse(await httpAddItem2.Content.ReadAsStringAsync());
        var httpItemLine2Id = httpAddItem2Payload.RootElement.GetProperty("data").GetProperty("id").GetInt32();

        using var httpPutItem = await http.PutAsJsonAsync($"/api/v1/sales/price-lists/{purchasePlId}/versions/{httpVersionId}/items/{httpItemLineId}", new { unitPrice = 89, minQuantity = 1, discountPercent = 5 });
        Check(httpPutItem.IsSuccessStatusCode, "PUT version items updates a Draft item over HTTP");
        Check((await http.DeleteAsync($"/api/v1/sales/price-lists/{purchasePlId}/versions/{httpVersionId}/items/{httpItemLine2Id}")).StatusCode == HttpStatusCode.NoContent,
            "DELETE version items removes a Draft item over HTTP");

        using var httpActivate = await http.PostAsync($"/api/v1/sales/price-lists/{purchasePlId}/versions/{httpVersionId}/activate", null);
        Check(httpActivate.IsSuccessStatusCode && JsonDocument.Parse(await httpActivate.Content.ReadAsStringAsync()).RootElement.GetProperty("data").GetProperty("status").GetInt32() == 2,
            "POST activate transitions a Draft version to Active over HTTP");

        using var editActiveDenied = await http.PutAsJsonAsync($"/api/v1/sales/price-lists/{purchasePlId}/versions/{httpVersionId}/items/{httpItemLineId}", new { unitPrice = 90, minQuantity = 1, discountPercent = 0 });
        var editActiveBody = await editActiveDenied.Content.ReadAsStringAsync();
        using var editActiveJson = JsonDocument.Parse(editActiveBody);
        Check(editActiveDenied.StatusCode == HttpStatusCode.Conflict
            && editActiveJson.RootElement.GetProperty("errorCode").GetString() == "error.sd.pricelist_version_not_draft"
            && editActiveJson.RootElement.GetProperty("message").GetString() != "error.sd.pricelist_version_not_draft",
            "editing an item on an Active version returns 409 with localized i18n message");

        using var activateAgainDenied = await http.PostAsync($"/api/v1/sales/price-lists/{purchasePlId}/versions/{httpVersionId}/activate", null);
        using var activateAgainJson = JsonDocument.Parse(await activateAgainDenied.Content.ReadAsStringAsync());
        Check(activateAgainDenied.StatusCode == HttpStatusCode.Conflict
            && activateAgainJson.RootElement.GetProperty("errorCode").GetString() == "error.sd.pricelist_version_invalid_status"
            && activateAgainJson.RootElement.GetProperty("message").GetString() != "error.sd.pricelist_version_invalid_status",
            "activating an already Active version returns 409 with localized invalid-status message");

        using var httpClose = await http.PostAsync($"/api/v1/sales/price-lists/{purchasePlId}/versions/{httpVersionId}/close", null);
        Check(httpClose.IsSuccessStatusCode && JsonDocument.Parse(await httpClose.Content.ReadAsStringAsync()).RootElement.GetProperty("data").GetProperty("status").GetInt32() == 3,
            "POST close transitions an Active version to Expired over HTTP");
        Check((await http.PostAsync($"/api/v1/sales/price-lists/{purchasePlId}/versions/{httpVersionId}/close", null)).StatusCode == HttpStatusCode.Conflict,
            "closing an Expired version is rejected with 409");
        Check((await http.DeleteAsync($"/api/v1/sales/price-lists/{purchasePlId}")).IsSuccessStatusCode, "temporary HTTP Purchase price list is cleaned up");
        Check((await http.GetAsync("/api/v1/sales/quotations")).IsSuccessStatusCode, "admin reads the quotation page over HTTP");
        Check((await http.PostAsync("/api/v1/sales/quotations/999999/convert-to-order", null)).StatusCode == HttpStatusCode.NotFound, "converting a missing quotation answers not found");
        Check((await http.PostAsJsonAsync("/api/v1/sales/orders/999999/cancel", new { reason = "x" })).StatusCode == HttpStatusCode.NotFound, "cancelling a missing order answers not found");
        Check((await http.PostAsJsonAsync("/api/v1/sales/orders/999999/approve-credit", new { reason = "x" })).StatusCode == HttpStatusCode.NotFound, "approving credit of a missing order answers not found");
        Check((await http.GetAsync("/api/v1/sales/returns")).IsSuccessStatusCode, "admin reads the sales return page over HTTP");
        Check((await http.GetAsync("/api/v1/sales/returns/999999")).StatusCode == HttpStatusCode.NotFound, "reading a missing return answers not found");
        Check((await http.GetAsync("/api/v1/sales/analytics/summary")).IsSuccessStatusCode && (await http.GetAsync("/api/v1/sales/analytics/salesperson-kpi")).IsSuccessStatusCode && (await http.GetAsync("/api/v1/sales/analytics/funnel")).IsSuccessStatusCode, "admin reads the three sales analytics endpoints over HTTP");
        Check((await http.PostAsJsonAsync("/api/v1/sales/orders/999999/create-invoice", new { warehouseId = 1, paymentMethod = 1, tenderedAmount = 0 })).StatusCode == HttpStatusCode.NotFound, "invoicing a missing order answers not found");
        Check((await http.GetAsync("/api/v1/sales/analytics/summary?from=2026-02-01T00:00:00Z&to=2026-01-01T00:00:00Z")).StatusCode == HttpStatusCode.BadRequest, "an inverted analytics period answers bad request");
        Check((await http.GetAsync("/api/v1/sales/invoices/999999")).StatusCode == HttpStatusCode.NotFound, "reading a missing invoice answers not found");
        http.DefaultRequestHeaders.Authorization = new("Bearer", viewerToken);
        Check((await http.PostAsync("/api/v1/sales/quotations/1/send", null)).StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden, "a user without sd:quotation:edit cannot send a quotation");
        Check((await http.PostAsJsonAsync("/api/v1/sales/orders/1/approve-credit", new { reason = "x" })).StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden && (await http.PostAsJsonAsync("/api/v1/sales/orders/1/cancel", new { reason = "x" })).StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden, "a user without the permission cannot approve credit or cancel an order");
        Check((await http.PostAsJsonAsync("/api/v1/sales/returns", new { branchId = 1, warehouseId = 1, refundMethod = 1, reason = "x", items = Array.Empty<object>() })).StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden, "a user without sd:return:create cannot post a return");
        http.DefaultRequestHeaders.Authorization = new("Bearer", adminToken);
        http.DefaultRequestHeaders.Authorization = new("Bearer", viewerToken);
        Check((await http.PostAsJsonAsync("/api/v1/sales/price-lists", new { code = "X", name = "X", currency = "VND", effectiveFrom = DateTime.UtcNow })).StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden, "a user without sd:pricelist:create cannot create a price list");
        Check((await http.GetAsync($"/api/v1/sales/price-lists/{vip.Id}/versions")).StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            && (await http.GetAsync($"/api/v1/sales/price-lists/{vip.Id}/versions/{vipVersionId}")).StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden,
            "a user without sd:pricelist:view cannot read price list versions");
        Check((await http.PostAsJsonAsync("/api/v1/sales/price-lists/1/versions/clone", new { sourceVersionId = 1, versionCode = "NO-ACCESS", effectiveFrom = DateTime.UtcNow })).StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden, "a user without sd:pricelist:edit cannot clone a version");
        Check((await http.PostAsJsonAsync("/api/v1/sales/price-lists/1/versions/1/bulk-adjustment", new { adjustmentType = 1, value = 1, roundingIncrement = 0.01m })).StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden, "a user without sd:pricelist:edit cannot bulk-adjust a version");
        http.DefaultRequestHeaders.Authorization = new("Bearer", adminToken);
        Check((await http.GetAsync("/api/v1/sys/notifications/preferences")).IsSuccessStatusCode, "admin reads preferences over HTTP");
        using var broadcastResp = await http.PostAsJsonAsync("/api/v1/sys/notifications/broadcast", new { title = "Broadcast", body = "System announcement", category = "SYS", severity = "INFO", targetType = "ALL" });
        Check(broadcastResp.IsSuccessStatusCode, "admin can broadcast notification over HTTP");
        using var iamCreated = await http.PostAsJsonAsync("/api/v1/auth/users", new { userName = "iam_http", displayName = "IAM HTTP", password, role = "None", branchIds = Array.Empty<int>() });
        Check(iamCreated.IsSuccessStatusCode, "an account without a legacy role can be provisioned");
        using var iamPayload = JsonDocument.Parse(await iamCreated.Content.ReadAsStringAsync());
        var iamUserId = iamPayload.RootElement.GetProperty("data").GetProperty("id").GetInt32();
        var company = await db.Branches.IgnoreQueryFilters().SingleAsync(b => b.Code == "COMPANY_A");
        var hanoi = await db.Branches.IgnoreQueryFilters().SingleAsync(b => b.Code == "CA_HN");
        var position = await db.JobPositions.SingleAsync(p => p.CompanyId == company.Id && p.Code == "SALES_ACC");
        using var hired = await http.PostAsJsonAsync("/api/v1/hrm/employees", new { code = "E-HTTP-01", fullName = "IAM HTTP", companyId = company.Id, branchId = hanoi.Id, jobPositionId = position.Id, userId = iamUserId });
        Check(hired.IsSuccessStatusCode, "admin hires an employee linked to the account");
        http.DefaultRequestHeaders.Authorization = new("Bearer", await Login("iam_http"));
        using var me = JsonDocument.Parse(await http.GetStringAsync("/api/v1/auth/me"));
        var permissions = me.RootElement.GetProperty("data").GetProperty("permissions").EnumerateArray().Select(p => p.GetString()).ToHashSet();
        Check(permissions.Contains("sd:order:view") && !permissions.Contains("sd:invoice:cancel") && me.RootElement.GetProperty("data").GetProperty("jobPositionId").GetInt32() == position.Id,
            "/auth/me returns the permissions inherited from the job position");
        using var scopedOrders = JsonDocument.Parse(await http.GetStringAsync("/api/v1/sales/orders"));
        var orderNumbers = scopedOrders.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().Select(o => o.GetProperty("orderNumber").GetString()).ToArray();
        Check(orderNumbers.SequenceEqual(["IAM-HN"]), "HTTP sales orders are limited to the position's BRANCH_TREE");
        using var cancelDenied = await http.PostAsJsonAsync("/api/v1/sales/invoices/1/cancel", new { reason = "IAM" });
        Check(cancelDenied.StatusCode == HttpStatusCode.Forbidden, "explicit Deny blocks invoice cancellation over HTTP");
        Check((await http.GetAsync("/api/v1/sys/permissions/catalog")).StatusCode == HttpStatusCode.Forbidden
            && (await http.PostAsJsonAsync("/api/v1/wms/categories", new { code = "X", name = "X" })).StatusCode == HttpStatusCode.Forbidden,
            "endpoints outside the inherited roles are forbidden");

        // CR-20-01: accounts are created together with their employee link.
        http.DefaultRequestHeaders.Authorization = new("Bearer", adminToken);
        var salesDepartment = await db.Departments.SingleAsync(d => d.CompanyId == company.Id && d.Code == "SALES");
        using var caHired = await http.PostAsJsonAsync("/api/v1/hrm/employees", new { code = "E-CA-ADM", fullName = "Company admin", companyId = company.Id, branchId = hanoi.Id, departmentId = salesDepartment.Id, jobPositionId = position.Id });
        using var caHiredPayload = JsonDocument.Parse(await caHired.Content.ReadAsStringAsync());
        var caEmployeeId = caHiredPayload.RootElement.GetProperty("data").GetProperty("id").GetInt32();
        using var free = JsonDocument.Parse(await http.GetStringAsync("/api/v1/hrm/employees?withoutAccount=true&pageSize=100"));
        Check(free.RootElement.GetProperty("data").GetProperty("items").EnumerateArray().Select(e => e.GetProperty("id").GetInt32()).Contains(caEmployeeId),
            "employees?withoutAccount=true lists employees without a login");
        using var caCreated = await http.PostAsJsonAsync("/api/v1/auth/users", new { userName = "ca_admin", displayName = "Company admin", password, role = "None", branchIds = Array.Empty<int>(), employeeId = caEmployeeId });
        using var caPayload = JsonDocument.Parse(await caCreated.Content.ReadAsStringAsync());
        var caUserId = caPayload.RootElement.GetProperty("data").GetProperty("id").GetInt32();
        Check(caCreated.IsSuccessStatusCode && caPayload.RootElement.GetProperty("data").GetProperty("employeeCode").GetString() == "E-CA-ADM",
            "a user is created together with its employee link");
        using var caGrant = await http.PutAsJsonAsync($"/api/v1/sys/roles/users/{caUserId}", new
        {
            roles = Array.Empty<object>(),
            inlinePolicy = new { statements = new[] { new { sid = "CompanyUsers", effect = "Allow", actions = new[] { "sys:user:*" }, dataScope = new { mode = "COMPANY" } } } }
        });
        Check(caGrant.IsSuccessStatusCode, "super administrator grants company user management");
        http.DefaultRequestHeaders.Authorization = new("Bearer", await Login("ca_admin"));
        using var caMe = JsonDocument.Parse(await http.GetStringAsync("/api/v1/auth/me"));
        var caData = caMe.RootElement.GetProperty("data");
        Check(caData.GetProperty("employeeId").GetInt32() == caEmployeeId && caData.GetProperty("companyId").GetInt32() == company.Id
            && caData.GetProperty("departmentId").GetInt32() == salesDepartment.Id
            && caData.GetProperty("permissions").EnumerateArray().Any(p => p.GetString() == "sd:order:create"),
            "/auth/me returns the linked employee, its org position and permissions granted by the department");
        using var unlinked = await http.PostAsJsonAsync("/api/v1/auth/users", new { userName = "ca_nobody", displayName = "No employee", password, role = "None", branchIds = Array.Empty<int>() });
        Check(unlinked.StatusCode == HttpStatusCode.BadRequest && (await unlinked.Content.ReadAsStringAsync()).Contains("error.validation.required"),
            "a company administrator cannot create a user without an employee");
        var takenEmployee = await db.Employees.Where(e => e.Code == "E-HTTP-01").Select(e => e.Id).SingleAsync();
        using var taken = await http.PostAsJsonAsync("/api/v1/auth/users", new { userName = "ca_dup", displayName = "Duplicate", password, role = "None", branchIds = Array.Empty<int>(), employeeId = takenEmployee });
        Check(taken.StatusCode == HttpStatusCode.Conflict && (await taken.Content.ReadAsStringAsync()).Contains("error.hrm.user_linked"),
            "a user cannot be linked to an employee that already has an account");

        // S25.2b: active user with sd:pricelist:view only can query ?type= but is forbidden on all version/item mutations.
        http.DefaultRequestHeaders.Authorization = new("Bearer", adminToken);
        using var plViewUserResp = await http.PostAsJsonAsync("/api/v1/auth/users", new { userName = "pl_viewer", displayName = "PriceList Viewer", password, role = "None", branchIds = Array.Empty<int>() });
        Check(plViewUserResp.IsSuccessStatusCode, "admin provisions price list viewer user");
        var plViewUserId = JsonDocument.Parse(await plViewUserResp.Content.ReadAsStringAsync()).RootElement.GetProperty("data").GetProperty("id").GetInt32();
        using var plViewGrant = await http.PutAsJsonAsync($"/api/v1/sys/roles/users/{plViewUserId}", new
        {
            roles = Array.Empty<object>(),
            inlinePolicy = new { statements = new[] { new { sid = "PriceListViewOnly", effect = "Allow", actions = new[] { "sd:pricelist:view" }, dataScope = new { mode = "ALL" } } } }
        });
        Check(plViewGrant.IsSuccessStatusCode, "super administrator grants sd:pricelist:view only");
        http.DefaultRequestHeaders.Authorization = new("Bearer", await Login("pl_viewer"));
        Check((await http.GetAsync($"/api/v1/sales/price-lists?type=Sales")).IsSuccessStatusCode, "user with sd:pricelist:view can filter price lists by type");
        Check((await http.PostAsJsonAsync($"/api/v1/sales/price-lists/{vip.Id}/versions", new { versionCode = "FORBID", effectiveFrom = DateTime.UtcNow })).StatusCode == HttpStatusCode.Forbidden
            && (await http.PutAsJsonAsync($"/api/v1/sales/price-lists/{vip.Id}/versions/{vipVersionId}", new { versionCode = "FORBID", effectiveFrom = DateTime.UtcNow })).StatusCode == HttpStatusCode.Forbidden
            && (await http.PostAsync($"/api/v1/sales/price-lists/{vip.Id}/versions/{vipVersionId}/activate", null)).StatusCode == HttpStatusCode.Forbidden
            && (await http.PostAsync($"/api/v1/sales/price-lists/{vip.Id}/versions/{vipVersionId}/close", null)).StatusCode == HttpStatusCode.Forbidden
            && (await http.PostAsJsonAsync($"/api/v1/sales/price-lists/{vip.Id}/versions/{vipVersionId}/items", new { itemId = 1, unitPrice = 1, minQuantity = 1 })).StatusCode == HttpStatusCode.Forbidden
            && (await http.PutAsJsonAsync($"/api/v1/sales/price-lists/{vip.Id}/versions/{vipVersionId}/items/1", new { unitPrice = 1, minQuantity = 1 })).StatusCode == HttpStatusCode.Forbidden
            && (await http.DeleteAsync($"/api/v1/sales/price-lists/{vip.Id}/versions/{vipVersionId}/items/1")).StatusCode == HttpStatusCode.Forbidden,
            "user without sd:pricelist:edit receives 403 Forbidden on all version and Draft item mutations");
    }
    finally
    {
        if (!api.HasExited) api.Kill(entireProcessTree: true);
        await api.WaitForExitAsync();
        var output = await stdout; var errors = await stderr;
        await File.WriteAllTextAsync(Path.Combine(AppContext.BaseDirectory, "api-verification.log"), output + errors);
        if (api.ExitCode != 0 && !string.IsNullOrWhiteSpace(errors)) Console.Error.WriteLine(errors);
    }
    Console.WriteLine($"VERIFIED {checks} checks. Database retained for inspection: {testName}");
}
catch { Console.Error.WriteLine($"Verification failed; isolated database retained: {testName}"); throw; }

/// <summary>Remembers what would have been published, so checks can read it.</summary>
sealed class RecordingEngine : ERPm.Application.Common.Notifications.INotificationEngine
{
    public List<ERPm.Domain.Events.INotificationEvent> Events { get; } = new();
    public Task<ERPm.Application.DTOs.System.NotificationResult> PublishAsync(ERPm.Domain.Events.INotificationEvent notifEvent, CancellationToken cancellationToken = default)
    {
        Events.Add(notifEvent);
        return Task.FromResult(new ERPm.Application.DTOs.System.NotificationResult(true, null, 0));
    }
    public Task<ERPm.Application.DTOs.System.NotificationResult> BroadcastAsync(ERPm.Application.DTOs.System.BroadcastNotificationRequest request, int? senderUserId = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();
}
/// <summary>Answers permission lookups from a fixed table, so rights can be tested without building roles.</summary>
sealed class IamStub(Dictionary<int, ERPm.Domain.Services.Iam.IamPrincipal> principals) : ERPm.Application.Features.System.IIamService
{
    public int CurrentUserId { get; set; }
    static ERPm.Domain.Services.Iam.IamPrincipal NoRights() => new([], new ERPm.Domain.Services.Iam.OrgContext { CompanyId = 1 }, false);
    public Task<ERPm.Domain.Services.Iam.IamPrincipal> GetPrincipalAsync(int userId, CancellationToken cancellationToken = default) => Task.FromResult(principals.TryGetValue(userId, out var p) ? p : NoRights());
    public Task<ERPm.Domain.Services.Iam.IamPrincipal?> GetCurrentPrincipalAsync(CancellationToken cancellationToken = default) => Task.FromResult<ERPm.Domain.Services.Iam.IamPrincipal?>(principals.TryGetValue(CurrentUserId, out var p) ? p : NoRights());
    public Task<List<ERPm.Application.DTOs.System.EffectivePermissionDto>> GetEffectiveAsync(int userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task EnsureCanGrantAsync(IEnumerable<ERPm.Domain.Entities.System.IamStatement> statements, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public void Invalidate() { }
}
sealed class ActorUser(int id) : ICurrentUser
{
    public string? UserId => id.ToString();
    public bool IsSystem => false;
    public bool IsAdmin => true;
    public int[] BranchIds => [];
}
/// <summary>A seller who only sees the records they created (SELF scope): no branch, own records on.</summary>
sealed class SelfUser : ICurrentUser
{
    public string? UserId => "seller";
    public bool IsSystem => false;
    public bool IsAdmin => false;
    public int[] BranchIds => [];
    public bool IncludeOwnRecords => true;
}
sealed class TestUser(bool admin = true, int[]? branches = null) : ICurrentUser
{
    public string? UserId => "verification";
    public bool IsSystem => false;
    public bool IsAdmin => admin;
    public int[] BranchIds => branches ?? [];
}
static class ModelCheck
{
    public static Task<bool> HasPendingModelChangesAsyncCompat(this Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade db) => Task.FromResult(db.HasPendingModelChanges());
}
