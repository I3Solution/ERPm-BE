using System.Text.Json;
using ERPm.Application.Common;
using ERPm.Application.Common.Crud;
using ERPm.Domain.Services.Iam;
using ERPm.Domain.Services.Sales;
using ERPm.Application.Common.Interfaces;
using ERPm.Application.DTOs.Inventory;
using ERPm.Application.DTOs.Sales;
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
    var dashboard = new DashboardService(db, new BranchService(db));
    var empty = await dashboard.GetMetricsAsync();
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
    var priceList = new PriceList { Code = "PL-RETAIL", Name = "Retail", Items = [new() { ItemId = item.Id, UnitPrice = 90, MinQuantity = 0 }, new() { ItemId = item.Id, UnitPrice = 80, MinQuantity = 10, DiscountPercent = 5 }] };
    db.PriceLists.Add(priceList); await db.SaveChangesAsync();
    Check((await db.PriceLists.Include(p => p.Items).SingleAsync(p => p.Code == "PL-RETAIL")).Items.Count == 2 && priceList.ExternalId.Version == 7, "price list keeps its quantity tiers");
    Check(await Rejected(async () => { db.PriceLists.Add(new PriceList { Code = "PL-RETAIL", Name = "Duplicate" }); await db.SaveChangesAsync(); }), "price list code is unique");
    Check(await Rejected(async () => { db.PriceListItems.Add(new PriceListItem { PriceListId = priceList.Id, ItemId = item.Id, UnitPrice = 70, MinQuantity = 10 }); await db.SaveChangesAsync(); }), "same item, unit and tier cannot be priced twice");
    Check(await Rejected(async () => { db.PriceListItems.Add(new PriceListItem { PriceListId = priceList.Id, ItemId = item.Id, UnitPrice = -1, MinQuantity = 20 }); await db.SaveChangesAsync(); }), "negative price is rejected");
    Check(await Rejected(async () => { db.PriceListItems.Add(new PriceListItem { PriceListId = priceList.Id, ItemId = item.Id, UnitPrice = 1, MinQuantity = 20, DiscountPercent = 101 }); await db.SaveChangesAsync(); }), "discount above 100 percent is rejected");
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
    Check(PermissionCatalog.Contains("sd:pricelist:view") && PermissionCatalog.Contains("sd:pricelist:delete"), "price list permissions are in the catalog");

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
    Check(await lists.UpdateAsync(999999, new UpdatePriceListRequest { Code = "X", Name = "X", Currency = "VND", EffectiveFrom = from }) == null, "updating a missing price list returns nothing");
    await Reject(async () => await lists.DeleteAsync(wholesale.Id), "error.sd.pricelist_in_use");
    await db.Partners.Where(p => p.Id == customer.Id).ExecuteUpdateAsync(p => p.SetProperty(c => c.PriceListId, (int?)null));
    Check(await lists.DeleteAsync(wholesale.Id) && await lists.GetByIdAsync(wholesale.Id) == null && !await lists.DeleteAsync(wholesale.Id), "an unused price list can be deleted once");
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
        using var order = await http.PostAsJsonAsync("/api/v1/sales/orders", new { branchId = branch.Id, customerId = customer.Id, discountAmount = 10, taxAmount = 5, items = new[] { new { itemId = item.Id, quantity = 2, unitPrice = 100 } } });
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
        http.DefaultRequestHeaders.Authorization = new("Bearer", viewerToken);
        Check((await http.PostAsJsonAsync("/api/v1/sales/price-lists", new { code = "X", name = "X", currency = "VND", effectiveFrom = DateTime.UtcNow })).StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden, "a user without sd:pricelist:create cannot create a price list");
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
