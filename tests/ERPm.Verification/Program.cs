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
    // Quotations: lifecycle, validity, conversion into a Draft order.
    var quotes = new SalesQuotationService(db, new PostingTransaction(db));
    var quoteDate = DateTime.UtcNow;
    CreateSalesQuotationRequest QuoteRequest(decimal quantity = 2, decimal discount = 20, DateTime? date = null, DateTime? until = null) => new()
    {
        QuotationDate = date ?? quoteDate, ValidUntil = until ?? quoteDate.AddDays(7), CustomerId = customer.Id, BranchId = branch.Id, TermsAndConditions = "Net 30",
        Items = [new() { ItemId = item.Id, Quantity = quantity, UnitPrice = 100, DiscountAmount = discount, Notes = "line" }]
    };
    var quote = await quotes.CreateAsync(QuoteRequest());
    Check(System.Text.RegularExpressions.Regex.IsMatch(quote.QuotationNumber, @"^BG-\d{6}-\d{4,}$") && quote.Status == "Draft" && quote.SubTotal == 200 && quote.DiscountAmount == 20 && quote.TotalAmount == 180
        && quote.CustomerName == "Customer" && quote.Items.Single().ItemCode == "ITEM" && quote.Items.Single().LineTotal == 180, "quotation is numbered, priced and snapshots customer and lines");
    await Reject(async () => await quotes.CreateAsync(new CreateSalesQuotationRequest { QuotationDate = quoteDate, ValidUntil = quoteDate, CustomerId = customer.Id, BranchId = branch.Id }), "error.validation.request");
    var toSupplier = QuoteRequest(); toSupplier.CustomerId = supplier.Id;
    await Reject(async () => await quotes.CreateAsync(toSupplier), "error.sales.customer");
    await Reject(async () => await quotes.CreateAsync(QuoteRequest(discount: 500)), "error.sales.discount");
    await Reject(async () => await quotes.CreateAsync(QuoteRequest(until: quoteDate.AddDays(-1))), "error.validation.request");
    var found = await quotes.GetPagedAsync(new BaseFilterQuery { Keyword = quote.QuotationNumber, Status = "Draft" });
    Check(found.TotalCount == 1 && found.Items.Single().Items.Count == 0 && found.Items.Single().BranchName == branch.Name, "quotation page filters by number and status and returns headers only");

    Check((await quotes.SendAsync(quote.Id)).Status == "Sent", "a draft quotation can be sent");
    await Reject(async () => await quotes.SendAsync(quote.Id), "error.sd.quotation_invalid_status");
    var revised = await quotes.UpdateAsync(quote.Id, new UpdateSalesQuotationRequest { QuotationDate = quoteDate, ValidUntil = quoteDate.AddDays(7), CustomerId = customer.Id, BranchId = branch.Id, Items = [new() { ItemId = item.Id, Quantity = 3, UnitPrice = 100 }] });
    Check(revised!.TotalAmount == 300 && revised.Items.Count == 1 && revised.Status == "Sent", "a sent quotation can be revised and its lines are replaced");
    Check((await quotes.AcceptAsync(quote.Id)).Status == "Accepted", "a sent quotation can be accepted");
    await Reject(async () => await quotes.UpdateAsync(quote.Id, new UpdateSalesQuotationRequest { QuotationDate = quoteDate, ValidUntil = quoteDate.AddDays(7), CustomerId = customer.Id, BranchId = branch.Id, Items = [new() { ItemId = item.Id, Quantity = 1, UnitPrice = 1 }] }), "error.sd.quotation_invalid_status");
    await Reject(async () => await quotes.DeleteAsync(quote.Id), "error.sd.quotation_invalid_status");

    var converted = await quotes.ConvertToOrderAsync(quote.Id);
    var convertedOrder = await db.SalesOrders.Include(o => o.Items).SingleAsync(o => o.Id == converted.SalesOrderId);
    Check(convertedOrder.Status == "Draft" && convertedOrder.TotalAmount == 300 && convertedOrder.Items.Single().Quantity == 3 && convertedOrder.CustomerId == customer.Id && convertedOrder.OrderNumber == converted.OrderNumber && convertedOrder.Notes == quote.QuotationNumber,
        "conversion creates a Draft order with the quotation lines");
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
    var twinInvoice = await Sales(db).CreateInvoiceAsync(OnCredit(limitedTwin.Id));
    Check(twinInvoice.RemainingDebt == 100, "once switched on, the customer's credit invoices are no longer blocked");
    await PartnersAs(holderB).SetUnlimitedCreditAsync(limitedTwin.Id, false);
    await Reject(async () => await Sales(db).CreateInvoiceAsync(OnCredit(limitedTwin.Id)), "error.sales.credit_limit");
    await Reject(async () => await PartnersAs(holderA).SetUnlimitedCreditAsync(999999, true), "error.crm.partner_not_found");

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
    for (var n = afterDelete.Count; n < PartnerDeliveryAddressService.MaxAddressesPerPartner; n++) await addresses.CreateAsync(addressee.Id, Address("Fill " + n));
    await Reject(async () => await addresses.CreateAsync(addressee.Id, Address("One too many")), "error.crm.delivery_address_limit");
    await addresses.DeleteAsync(addressee.Id, (await addresses.GetAsync(addressee.Id)).Last().Id);
    Check((await addresses.CreateAsync(addressee.Id, Address("Fits again"))).PartnerId == addressee.Id, "deleting an address frees a slot under the limit");
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
    { BranchId = branch.Id, CustomerId = customerId, DiscountAmount = discount, TaxAmount = tax, Items = [new() { ItemId = inv.Id, Quantity = quantity, UnitPrice = price, DiscountAmount = lineDiscount }] });
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
    var retail = await orders.ConfirmAsync((await InvOrder(null, 2, 100)).Id, default);
    var heldBefore = (await InvStock()).QuantityReserved;
    await Reject(async () => await seller3.CreateInvoiceFromOrderAsync(retail.Id, FromOrder(PaymentMethod.Debt)), "error.sales.customer_required");
    Check((await InvStock()).QuantityReserved == heldBefore && (await orders.GetByIdAsync(retail.Id))!.Status == "Confirmed", "a refused invoice leaves the order Confirmed with its stock still held");
    (await db.Accounts.SingleAsync(a => a.Code == "511")).IsActive = false; await db.SaveChangesAsync();   // restore the state the next checks expect
    // Quotation notifications follow the customer's answer and the conversion, and only for changes that were saved.
    var recorder = new RecordingEngine();
    var talking = new SalesQuotationService(db, new PostingTransaction(db), recorder);
    var announced = await talking.CreateAsync(QuoteRequest());
    await talking.SendAsync(announced.Id);
    Check(recorder.Events.Count == 0, "sending a quotation notifies nobody");
    await talking.AcceptAsync(announced.Id);
    Check(recorder.Events.Count == 1 && recorder.Events[0].TemplateCode == "SD_QUOTATION_ACCEPTED" && recorder.Events[0].BranchId == branch.Id && recorder.Events[0].Parameters["quotation_code"].ToString() == announced.QuotationNumber,
        "an accepted quotation notifies its branch");
    var conversion = await talking.ConvertToOrderAsync(announced.Id);
    Check(recorder.Events.Count == 2 && recorder.Events[1].TemplateCode == "SD_QUOTATION_CONVERTED" && recorder.Events[1].Parameters["order_code"].ToString() == conversion.OrderNumber, "a converted quotation notifies with the new order number");
    await Reject(async () => await talking.ConvertToOrderAsync(announced.Id), "error.sd.quotation_already_converted");
    var turnedDown = await talking.CreateAsync(QuoteRequest());
    await talking.SendAsync(turnedDown.Id);
    await talking.RejectAsync(turnedDown.Id);
    Check(recorder.Events.Count == 3 && recorder.Events[2].TemplateCode == "SD_QUOTATION_REJECTED" && recorder.Events[2].Severity == "WARNING", "a rejected quotation raises a warning");
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
