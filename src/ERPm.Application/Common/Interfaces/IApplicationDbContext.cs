using ERPm.Domain.Entities.Accounting;
using ERPm.Domain.Entities.Catalog;
using ERPm.Domain.Entities.Inventory;
using ERPm.Domain.Entities.Localization;
using ERPm.Domain.Entities.Partner;
using ERPm.Domain.Entities.Purchase;
using ERPm.Domain.Entities.Sales;
using ERPm.Domain.Entities.System;
using Microsoft.EntityFrameworkCore;

namespace ERPm.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    // System / Organization
    DbSet<Branch> Branches { get; }

    // Localization
    DbSet<AppLanguage> AppLanguages { get; }
    DbSet<AppDictionary> AppDictionaries { get; }

    // Catalog / ITEM
    DbSet<UnitOfMeasure> UnitsOfMeasure { get; }
    DbSet<Category> Categories { get; }
    DbSet<Item> Items { get; }
    DbSet<ItemAttribute> ItemAttributes { get; }

    // Partner (Customers & Suppliers)
    DbSet<Partner> Partners { get; }

    // Inventory
    DbSet<Warehouse> Warehouses { get; }
    DbSet<StockBalance> StockBalances { get; }
    DbSet<StockReceipt> StockReceipts { get; }
    DbSet<StockReceiptItem> StockReceiptItems { get; }
    DbSet<StockIssue> StockIssues { get; }
    DbSet<StockIssueItem> StockIssueItems { get; }

    // Sales & Invoicing
    DbSet<SalesOrder> SalesOrders { get; }
    DbSet<SalesOrderItem> SalesOrderItems { get; }
    DbSet<SalesInvoice> SalesInvoices { get; }
    DbSet<SalesInvoiceItem> SalesInvoiceItems { get; }

    // Purchasing
    DbSet<PurchaseOrder> PurchaseOrders { get; }
    DbSet<PurchaseOrderItem> PurchaseOrderItems { get; }

    // Accounting Foundation
    DbSet<Account> Accounts { get; }
    DbSet<JournalEntry> JournalEntries { get; }
    DbSet<JournalEntryLine> JournalEntryLines { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
