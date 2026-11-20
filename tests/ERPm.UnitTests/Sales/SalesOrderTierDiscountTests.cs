using ERPm.Application.Common;
using ERPm.Application.DTOs.Sales;
using ERPm.Domain.Entities.Catalog;
using ERPm.Domain.Entities.Inventory;
using ERPm.Domain.Entities.Partner;
using ERPm.Domain.Entities.Sales;
using ERPm.Domain.Entities.System;
using ERPm.Domain.Services.Sales;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Services;
using ERPm.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace ERPm.UnitTests.Sales;

public sealed class SalesOrderTierDiscountTests
{
    private static ApplicationDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<(Branch BranchA, Branch BranchB, UnitOfMeasure Pcs, UnitOfMeasure Box, Item Item, Partner Customer)> SeedBaseAsync(ApplicationDbContext db)
    {
        var pcs = new UnitOfMeasure { Id = 1, Code = "PCS", Name = "Piece", IsActive = true };
        var box = new UnitOfMeasure { Id = 2, Code = "BOX", Name = "Box", IsActive = true };
        var branchA = new Branch { Id = 1, Code = "BR-A", Name = "Branch A", IsActive = true };
        var branchB = new Branch { Id = 2, Code = "BR-B", Name = "Branch B", IsActive = true };
        var item = new Item
        {
            Id = 2,
            Code = "SP02",
            Name = "Product 2",
            BaseUomId = pcs.Id,
            SalePrice = 10_000m,
            VatRate = 0m,
            IsActive = true
        };
        var customer = new Partner
        {
            Id = 20,
            Code = "KH01",
            Name = "Customer 1",
            PartnerType = PartnerType.Customer,
            CreditLimit = 10_000_000m,
            IsActive = true
        };
        var warehouse = new Warehouse
        {
            Id = 1,
            Code = "WH-A",
            Name = "Warehouse A",
            BranchId = branchA.Id,
            IsActive = true
        };
        var serving = new BranchServingWarehouse
        {
            Id = 1,
            BranchId = branchA.Id,
            WarehouseId = warehouse.Id,
            IsDefault = true
        };
        var stock = new StockBalance
        {
            Id = 1,
            WarehouseId = warehouse.Id,
            ItemId = item.Id,
            QuantityOnHand = 1_000m,
            QuantityReserved = 0m
        };

        db.UnitsOfMeasure.AddRange(pcs, box);
        db.Branches.AddRange(branchA, branchB);
        db.Items.Add(item);
        db.Partners.Add(customer);
        db.Warehouses.Add(warehouse);
        db.BranchServingWarehouses.Add(serving);
        db.StockBalances.Add(stock);
        await db.SaveChangesAsync();
        return (branchA, branchB, pcs, box, item, customer);
    }

    private static async Task<(PriceList List, PriceListVersion Version)> CreateActiveSalesPriceListAsync(
        ApplicationDbContext db,
        string code,
        string name,
        int? branchId,
        DateTime effectiveFrom,
        params PriceListItem[] items)
    {
        var list = new PriceList
        {
            Code = code,
            Name = name,
            Currency = "VND",
            Type = PriceListType.Sales,
            EffectiveFrom = effectiveFrom,
            BranchId = branchId,
            IsActive = true
        };
        var version = new PriceListVersion
        {
            PriceList = list,
            VersionCode = "V1",
            EffectiveFrom = effectiveFrom,
            Status = PriceListVersionStatus.Active,
            Items = items.ToList()
        };
        list.Versions.Add(version);
        db.PriceLists.Add(list);
        await db.SaveChangesAsync();
        return (list, version);
    }

    [Fact]
    public void PricingEngine_PercentageTierBoundaries_99_100_101_99()
    {
        var candidate = new PriceCandidate(
            PriceListId: 1,
            PriceListName: "Percent Tier",
            IsCustomerDefault: false,
            IsBranchSpecific: true,
            EffectiveFrom: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Tiers:
            [
                new PriceTier(0m, 10_000m, 0m, 0m),
                new PriceTier(100m, 10_000m, 1m, 0m)
            ]);

        var q99 = PricingEngine.Price(10_000m, 99m, [candidate]);
        var q100 = PricingEngine.Price(10_000m, 100m, [candidate]);
        var q101 = PricingEngine.Price(10_000m, 101m, [candidate]);
        var q99Again = PricingEngine.Price(10_000m, 99m, [candidate]);

        Assert.Equal(0m, q99.DiscountPercent);
        Assert.Equal(0m, q99.DiscountAmount);
        Assert.Equal(990_000m, q99.LineTotal);

        Assert.Equal(1m, q100.DiscountPercent);
        Assert.Equal(10_000m, q100.DiscountAmount);
        Assert.Equal(990_000m, q100.LineTotal);

        Assert.Equal(1m, q101.DiscountPercent);
        Assert.Equal(10_100m, q101.DiscountAmount);
        Assert.Equal(999_900m, q101.LineTotal);

        Assert.Equal(0m, q99Again.DiscountPercent);
        Assert.Equal(0m, q99Again.DiscountAmount);
        Assert.Equal(990_000m, q99Again.LineTotal);
    }

    [Fact]
    public void PricingEngine_FixedAmountTierBoundaries_99_100_101_99()
    {
        var candidate = new PriceCandidate(
            PriceListId: 2,
            PriceListName: "Fixed Tier",
            IsCustomerDefault: false,
            IsBranchSpecific: true,
            EffectiveFrom: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Tiers:
            [
                new PriceTier(0m, 10_000m, 0m, 0m),
                new PriceTier(100m, 10_000m, 0m, 10_000m)
            ]);

        var q99 = PricingEngine.Price(10_000m, 99m, [candidate]);
        var q100 = PricingEngine.Price(10_000m, 100m, [candidate]);
        var q101 = PricingEngine.Price(10_000m, 101m, [candidate]);
        var q99Again = PricingEngine.Price(10_000m, 99m, [candidate]);

        Assert.Equal(0m, q99.FixedDiscountAmount);
        Assert.Equal(0m, q99.DiscountAmount);
        Assert.Equal(990_000m, q99.LineTotal);

        Assert.Equal(10_000m, q100.FixedDiscountAmount);
        Assert.Equal(10_000m, q100.DiscountAmount);
        Assert.Equal(990_000m, q100.LineTotal);

        Assert.Equal(10_000m, q101.FixedDiscountAmount);
        Assert.Equal(10_000m, q101.DiscountAmount);
        Assert.Equal(1_000_000m, q101.LineTotal);

        Assert.Equal(0m, q99Again.FixedDiscountAmount);
        Assert.Equal(0m, q99Again.DiscountAmount);
        Assert.Equal(990_000m, q99Again.LineTotal);
    }

    [Fact]
    public void PricingEngine_CombinedPercentAndFixedDiscount_CapsAtGross()
    {
        var combined = new PriceCandidate(
            PriceListId: 3,
            PriceListName: "Combined",
            IsCustomerDefault: false,
            IsBranchSpecific: false,
            EffectiveFrom: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Tiers: [new PriceTier(100m, 10_000m, 1m, 5_000m)]);

        var res = PricingEngine.Price(10_000m, 100m, [combined]);
        Assert.Equal(1m, res.DiscountPercent);
        Assert.Equal(5_000m, res.FixedDiscountAmount);
        Assert.Equal(15_000m, res.DiscountAmount);
        Assert.Equal(985_000m, res.LineTotal);

        var oversized = new PriceCandidate(
            PriceListId: 4,
            PriceListName: "Oversized",
            IsCustomerDefault: false,
            IsBranchSpecific: false,
            EffectiveFrom: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Tiers: [new PriceTier(0m, 10_000m, 0m, 25_000m)]);

        var capped = PricingEngine.Price(10_000m, 1m, [oversized]);
        Assert.Equal(10_000m, capped.DiscountAmount);
        Assert.Equal(0m, capped.LineTotal);
    }

    [Fact]
    public async Task CalculateAsync_Precedence_ExplicitThenCustomerThenDefault_AndUomMatching()
    {
        await using var db = CreateDb(nameof(CalculateAsync_Precedence_ExplicitThenCustomerThenDefault_AndUomMatching));
        var (branchA, branchB, pcs, box, item, customer) = await SeedBaseAsync(db);
        var effective = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var (defaultList, _) = await CreateActiveSalesPriceListAsync(db, "PL-DEF", "Default Cheapest", branchA.Id, effective,
            new PriceListItem { ItemId = item.Id, UomId = null, MinQuantity = 0m, UnitPrice = 8_000m, DiscountPercent = 0m, DiscountAmount = 0m });
        var (customerList, _) = await CreateActiveSalesPriceListAsync(db, "PL-CUST", "Customer List", branchA.Id, effective,
            new PriceListItem { ItemId = item.Id, UomId = null, MinQuantity = 0m, UnitPrice = 9_000m, DiscountPercent = 0m, DiscountAmount = 500m });
        var (explicitList, _) = await CreateActiveSalesPriceListAsync(db, "PL-EXP", "Explicit List", branchA.Id, effective,
            new PriceListItem { ItemId = item.Id, UomId = null, MinQuantity = 0m, UnitPrice = 9_500m, DiscountPercent = 0m, DiscountAmount = 0m },
            new PriceListItem { ItemId = item.Id, UomId = box.Id, MinQuantity = 0m, UnitPrice = 90_000m, DiscountPercent = 0m, DiscountAmount = 2_000m });
        var (otherBranchList, _) = await CreateActiveSalesPriceListAsync(db, "PL-BRB", "Branch B List", branchB.Id, effective,
            new PriceListItem { ItemId = item.Id, UomId = null, MinQuantity = 0m, UnitPrice = 7_000m, DiscountPercent = 0m, DiscountAmount = 0m });

        customer.PriceListId = customerList.Id;
        await db.SaveChangesAsync();

        var service = new PriceListService(db);

        // 1. Explicit wins over customer and default.
        var explicitRes = await service.CalculateAsync(new CalculatePriceRequest
        {
            PriceListId = explicitList.Id,
            CustomerId = customer.Id,
            BranchId = branchA.Id,
            DocumentDate = effective.AddDays(1),
            Items = [new() { ItemId = item.Id, Quantity = 1m }]
        });
        Assert.Equal(explicitList.Id, explicitRes.Items[0].AppliedPriceListId);
        Assert.Equal(9_500m, explicitRes.Items[0].UnitPrice);

        // 2. Per-line UomId matches Box tier (previously ignored when l.UomId == null was hardcoded).
        var boxRes = await service.CalculateAsync(new CalculatePriceRequest
        {
            PriceListId = explicitList.Id,
            CustomerId = customer.Id,
            BranchId = branchA.Id,
            DocumentDate = effective.AddDays(1),
            Items = [new() { ItemId = item.Id, UomId = box.Id, Quantity = 1m }]
        });
        Assert.Equal(explicitList.Id, boxRes.Items[0].AppliedPriceListId);
        Assert.Equal(box.Id, boxRes.Items[0].UomId);
        Assert.Equal(90_000m, boxRes.Items[0].UnitPrice);
        Assert.Equal(2_000m, boxRes.Items[0].FixedDiscountAmount);
        Assert.Equal(2_000m, boxRes.Items[0].DiscountAmount);
        Assert.Equal(88_000m, boxRes.Items[0].LineTotal);

        // 3. No explicit -> customer list wins over cheaper default list.
        var customerRes = await service.CalculateAsync(new CalculatePriceRequest
        {
            CustomerId = customer.Id,
            BranchId = branchA.Id,
            DocumentDate = effective.AddDays(1),
            Items = [new() { ItemId = item.Id, UomId = pcs.Id, Quantity = 1m }]
        });
        Assert.Equal(customerList.Id, customerRes.Items[0].AppliedPriceListId);
        Assert.Equal(9_000m, customerRes.Items[0].UnitPrice);
        Assert.Equal(500m, customerRes.Items[0].DiscountAmount);

        // 4. No explicit and no customer -> default list wins.
        var defaultRes = await service.CalculateAsync(new CalculatePriceRequest
        {
            BranchId = branchA.Id,
            DocumentDate = effective.AddDays(1),
            Items = [new() { ItemId = item.Id, Quantity = 1m }]
        });
        Assert.Equal(defaultList.Id, defaultRes.Items[0].AppliedPriceListId);
        Assert.Equal(8_000m, defaultRes.Items[0].UnitPrice);

        // 5. Explicit list from another branch is rejected.
        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.CalculateAsync(new CalculatePriceRequest
        {
            PriceListId = otherBranchList.Id,
            BranchId = branchA.Id,
            DocumentDate = effective.AddDays(1),
            Items = [new() { ItemId = item.Id, Quantity = 1m }]
        }));
        Assert.Equal("error.validation.reference", ex.Code);
    }

    [Fact]
    public async Task PriceListItemCrud_PersistsAndClonesDiscountAmount()
    {
        await using var db = CreateDb(nameof(PriceListItemCrud_PersistsAndClonesDiscountAmount));
        var (branchA, _, _, _, item, _) = await SeedBaseAsync(db);
        var service = new PriceListService(db);
        var effective = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var created = await service.CreateAsync(new CreatePriceListRequest
        {
            Code = "PL-CRUD",
            Name = "CRUD Fixed Discount",
            BranchId = branchA.Id,
            EffectiveFrom = effective,
            Items = [new() { ItemId = item.Id, MinQuantity = 0m, UnitPrice = 10_000m, DiscountPercent = 0m, DiscountAmount = 1_000m }]
        });
        var versions = await service.GetVersionsAsync(created.Id);
        var v1 = await service.GetVersionAsync(created.Id, versions.Single().Id);
        Assert.Equal(1_000m, v1.Items.Single().DiscountAmount);

        var added = await service.AddVersionItemAsync(created.Id, v1.Id, new CreatePriceListItemRequest
        {
            ItemId = item.Id,
            MinQuantity = 100m,
            UnitPrice = 10_000m,
            DiscountPercent = 0m,
            DiscountAmount = 10_000m
        });
        Assert.Equal(10_000m, added.DiscountAmount);

        var updated = await service.UpdateVersionItemAsync(created.Id, v1.Id, added.Id!.Value, new UpdatePriceListItemRequest
        {
            ItemId = item.Id,
            MinQuantity = 100m,
            UnitPrice = 10_000m,
            DiscountPercent = 1m,
            DiscountAmount = 12_000m
        });
        Assert.Equal(1m, updated.DiscountPercent);
        Assert.Equal(12_000m, updated.DiscountAmount);

        var cloned = await service.CloneVersionAsync(created.Id, new ClonePriceListVersionRequest
        {
            SourceVersionId = v1.Id,
            VersionCode = "V2",
            EffectiveFrom = effective.AddMonths(1)
        });
        Assert.Contains(cloned.Items, i => i.MinQuantity == 100m && i.DiscountAmount == 12_000m && i.DiscountPercent == 1m);

        var ex = await Assert.ThrowsAsync<BusinessException>(() => service.AddVersionItemAsync(created.Id, v1.Id, new CreatePriceListItemRequest
        {
            ItemId = item.Id,
            MinQuantity = 200m,
            UnitPrice = 10_000m,
            DiscountPercent = 0m,
            DiscountAmount = -500m
        }));
        Assert.Equal("error.validation.number", ex.Code);
    }

    [Fact]
    public async Task SalesOrder_TierBoundaries_99_100_101_99_AndSnapshotPreservedAfterPriceListClosed()
    {
        await using var db = CreateDb(nameof(SalesOrder_TierBoundaries_99_100_101_99_AndSnapshotPreservedAfterPriceListClosed));
        var (branchA, _, _, _, item, customer) = await SeedBaseAsync(db);
        var effective = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var orderDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        var (pctList, pctVersion) = await CreateActiveSalesPriceListAsync(db, "PL-PCT", "Percent List", branchA.Id, effective,
            new PriceListItem { ItemId = item.Id, MinQuantity = 0m, UnitPrice = 10_000m, DiscountPercent = 0m, DiscountAmount = 0m },
            new PriceListItem { ItemId = item.Id, MinQuantity = 100m, UnitPrice = 10_000m, DiscountPercent = 1m, DiscountAmount = 0m });

        var (fixedList, fixedVersion) = await CreateActiveSalesPriceListAsync(db, "PL-FIX", "Fixed List", branchA.Id, effective,
            new PriceListItem { ItemId = item.Id, MinQuantity = 0m, UnitPrice = 10_000m, DiscountPercent = 0m, DiscountAmount = 0m },
            new PriceListItem { ItemId = item.Id, MinQuantity = 100m, UnitPrice = 10_000m, DiscountPercent = 0m, DiscountAmount = 10_000m });

        var orders = new SalesOrderService(db, new PostingTransaction(db));
        var priceLists = new PriceListService(db);

        // Percentage tier on SO: 99 -> 100 -> 101 -> 99
        var soPct = await orders.CreateAsync(new CreateSalesOrderRequest
        {
            OrderNumber = "SO-PCT-01",
            OrderDate = orderDate,
            BranchId = branchA.Id,
            WarehouseId = 1,
            CustomerId = customer.Id,
            PriceListId = pctList.Id,
            Items = [new() { ItemId = item.Id, Quantity = 99m }]
        });
        Assert.Equal(pctList.Id, soPct.PriceListId);
        Assert.Equal(0m, soPct.Items[0].DiscountAmount);
        Assert.Equal(990_000m, soPct.TotalAmount);
        Assert.Equal(pctVersion.Id, soPct.Items[0].AppliedPriceListVersionId);

        soPct = (await orders.UpdateAsync(soPct.Id, new UpdateSalesOrderRequest
        {
            OrderDate = orderDate,
            BranchId = branchA.Id,
            WarehouseId = 1,
            CustomerId = customer.Id,
            PriceListId = pctList.Id,
            Items = [new() { ItemId = item.Id, Quantity = 100m }]
        }))!;
        Assert.Equal(1m, soPct.Items[0].DiscountPercent);
        Assert.Equal(10_000m, soPct.Items[0].DiscountAmount);
        Assert.Equal(990_000m, soPct.TotalAmount);

        soPct = (await orders.UpdateAsync(soPct.Id, new UpdateSalesOrderRequest
        {
            OrderDate = orderDate,
            BranchId = branchA.Id,
            WarehouseId = 1,
            CustomerId = customer.Id,
            PriceListId = pctList.Id,
            Items = [new() { ItemId = item.Id, Quantity = 101m }]
        }))!;
        Assert.Equal(10_100m, soPct.Items[0].DiscountAmount);
        Assert.Equal(999_900m, soPct.TotalAmount);

        soPct = (await orders.UpdateAsync(soPct.Id, new UpdateSalesOrderRequest
        {
            OrderDate = orderDate,
            BranchId = branchA.Id,
            WarehouseId = 1,
            CustomerId = customer.Id,
            PriceListId = pctList.Id,
            Items = [new() { ItemId = item.Id, Quantity = 99m }]
        }))!;
        Assert.Equal(0m, soPct.Items[0].DiscountAmount);
        Assert.Equal(990_000m, soPct.TotalAmount);

        // Fixed amount tier on SO: 99 -> 100 -> 101 -> 99 -> 100 -> Confirm -> Close Price List -> Reload
        var soFix = await orders.CreateAsync(new CreateSalesOrderRequest
        {
            OrderNumber = "SO-FIX-01",
            OrderDate = orderDate,
            BranchId = branchA.Id,
            WarehouseId = 1,
            CustomerId = customer.Id,
            PriceListId = fixedList.Id,
            Items = [new() { ItemId = item.Id, Quantity = 99m }]
        });
        Assert.Equal(0m, soFix.Items[0].DiscountAmount);
        Assert.Equal(990_000m, soFix.TotalAmount);

        soFix = (await orders.UpdateAsync(soFix.Id, new UpdateSalesOrderRequest
        {
            OrderDate = orderDate,
            BranchId = branchA.Id,
            WarehouseId = 1,
            CustomerId = customer.Id,
            PriceListId = fixedList.Id,
            Items = [new() { ItemId = item.Id, Quantity = 100m }]
        }))!;
        Assert.Equal(10_000m, soFix.Items[0].FixedDiscountAmount);
        Assert.Equal(10_000m, soFix.Items[0].DiscountAmount);
        Assert.Equal(990_000m, soFix.TotalAmount);

        soFix = (await orders.UpdateAsync(soFix.Id, new UpdateSalesOrderRequest
        {
            OrderDate = orderDate,
            BranchId = branchA.Id,
            WarehouseId = 1,
            CustomerId = customer.Id,
            PriceListId = fixedList.Id,
            Items = [new() { ItemId = item.Id, Quantity = 101m }]
        }))!;
        Assert.Equal(10_000m, soFix.Items[0].FixedDiscountAmount);
        Assert.Equal(10_000m, soFix.Items[0].DiscountAmount);
        Assert.Equal(1_000_000m, soFix.TotalAmount);

        soFix = (await orders.UpdateAsync(soFix.Id, new UpdateSalesOrderRequest
        {
            OrderDate = orderDate,
            BranchId = branchA.Id,
            WarehouseId = 1,
            CustomerId = customer.Id,
            PriceListId = fixedList.Id,
            Items = [new() { ItemId = item.Id, Quantity = 99m }]
        }))!;
        Assert.Equal(0m, soFix.Items[0].DiscountAmount);

        // Set back to 100 and confirm.
        soFix = (await orders.UpdateAsync(soFix.Id, new UpdateSalesOrderRequest
        {
            OrderDate = orderDate,
            BranchId = branchA.Id,
            WarehouseId = 1,
            CustomerId = customer.Id,
            PriceListId = fixedList.Id,
            Items = [new() { ItemId = item.Id, Quantity = 100m }]
        }))!;
        var confirmed = await orders.ConfirmAsync(soFix.Id, CancellationToken.None);
        Assert.Equal(SalesOrderStatus.Confirmed, confirmed.Status);

        // Close the price list version and verify the confirmed SO keeps its snapshot intact.
        await priceLists.CloseVersionAsync(fixedList.Id, fixedVersion.Id, new PriceListVersionTransitionRequest { Reason = "Superseded" });

        var reloaded = (await orders.GetByIdAsync(soFix.Id))!;
        var line = reloaded.Items.Single();
        Assert.Equal(SalesOrderStatus.Confirmed, reloaded.Status);
        Assert.Equal(fixedList.Id, reloaded.PriceListId);
        Assert.Equal(fixedList.Id, line.AppliedPriceListId);
        Assert.Equal("Fixed List", line.AppliedPriceListName);
        Assert.Equal(fixedVersion.Id, line.AppliedPriceListVersionId);
        Assert.Equal("V1", line.AppliedVersionCode);
        Assert.Equal(10_000m, line.OriginalPrice);
        Assert.Equal(10_000m, line.UnitPrice);
        Assert.Equal(10_000m, line.FixedDiscountAmount);
        Assert.Equal(10_000m, line.DiscountAmount);
        Assert.Equal(990_000m, line.LineTotal);
        Assert.Equal(990_000m, reloaded.TotalAmount);
    }

    [Fact]
    public async Task SalesOrder_ApplyPriceListTrue_UsesCustomerList_AndManualOrderKeepsCallerPrices()
    {
        await using var db = CreateDb(nameof(SalesOrder_ApplyPriceListTrue_UsesCustomerList_AndManualOrderKeepsCallerPrices));
        var (branchA, _, _, _, item, customer) = await SeedBaseAsync(db);
        var effective = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var orderDate = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        var (custList, custVersion) = await CreateActiveSalesPriceListAsync(db, "PL-CUST2", "VIP List", branchA.Id, effective,
            new PriceListItem { ItemId = item.Id, MinQuantity = 10m, UnitPrice = 9_500m, DiscountPercent = 2m, DiscountAmount = 500m });
        customer.PriceListId = custList.Id;
        await db.SaveChangesAsync();

        var orders = new SalesOrderService(db, new PostingTransaction(db));

        // Auto-apply without explicit PriceListId picks customer's VIP list.
        var autoOrder = await orders.CreateAsync(new CreateSalesOrderRequest
        {
            OrderNumber = "SO-AUTO-01",
            OrderDate = orderDate,
            BranchId = branchA.Id,
            WarehouseId = 1,
            CustomerId = customer.Id,
            ApplyPriceList = true,
            Items = [new() { ItemId = item.Id, Quantity = 10m }]
        });
        var autoLine = autoOrder.Items.Single();
        Assert.Equal(custList.Id, autoLine.AppliedPriceListId);
        Assert.Equal(custVersion.Id, autoLine.AppliedPriceListVersionId);
        Assert.Equal(9_500m, autoLine.UnitPrice);
        Assert.Equal(2m, autoLine.DiscountPercent);
        Assert.Equal(500m, autoLine.FixedDiscountAmount);
        Assert.Equal(2_400m, autoLine.DiscountAmount); // 2% of 95,000 (=1,900) + 500 = 2,400
        Assert.Equal(92_600m, autoLine.LineTotal);

        // Legacy/manual order keeps caller-provided UnitPrice and DiscountAmount.
        var manualOrder = await orders.CreateAsync(new CreateSalesOrderRequest
        {
            OrderNumber = "SO-MAN-01",
            OrderDate = orderDate,
            BranchId = branchA.Id,
            WarehouseId = 1,
            CustomerId = customer.Id,
            Items = [new() { ItemId = item.Id, Quantity = 10m, UnitPrice = 12_000m, DiscountAmount = 1_000m }]
        });
        var manualLine = manualOrder.Items.Single();
        Assert.Null(manualLine.AppliedPriceListId);
        Assert.Equal(12_000m, manualLine.UnitPrice);
        Assert.Equal(1_000m, manualLine.DiscountAmount);
        Assert.Equal(119_000m, manualLine.LineTotal);
    }
}
