using ERPm.Application.Common.Interfaces;
using ERPm.Application.DTOs.Dashboard;
using ERPm.Application.Features.Dashboard;
using ERPm.Application.Features.System;
using Microsoft.EntityFrameworkCore;
using ERPm.Domain.Entities.Accounting;

namespace ERPm.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly IApplicationDbContext _context;
    private readonly IBranchService _branchService;

    public DashboardService(IApplicationDbContext context, IBranchService branchService)
    {
        _context = context;
        _branchService = branchService;
    }

    public async Task<DashboardMetricsDto> GetMetricsAsync(int? branchId = null, CancellationToken cancellationToken = default)
    {
        var today = DateTime.SpecifyKind(DateTime.UtcNow.AddHours(7).Date.AddHours(-7), DateTimeKind.Utc);
        var tomorrow = today.AddDays(1);
        List<int>? targetBranchIds = null;

        if (branchId.HasValue && branchId.Value > 0)
        {
            targetBranchIds = await _branchService.GetBranchAndDescendantIdsAsync(branchId.Value, cancellationToken);
        }

        var scopedInvoices = _context.SalesInvoices.AsNoTracking().Where(i => i.Status == VoucherStatus.Posted);
        var journalQuery = _context.JournalEntries.AsNoTracking();
        var balanceQuery = _context.StockBalances.Include(b => b.Warehouse).AsNoTracking();

        if (targetBranchIds != null && targetBranchIds.Count > 0)
        {
            scopedInvoices = scopedInvoices.Where(i => targetBranchIds.Contains(i.BranchId));
            journalQuery = journalQuery.Where(j => targetBranchIds.Contains(j.BranchId));
            balanceQuery = balanceQuery.Where(b => b.Warehouse != null && targetBranchIds.Contains(b.Warehouse.BranchId));
        }

        var todayInvoices = await scopedInvoices.Where(i => i.InvoiceDate >= today && i.InvoiceDate < tomorrow).ToListAsync(cancellationToken);
        var todaySales = todayInvoices.Sum(i => i.TotalAmount);
        var todayCount = todayInvoices.Count;

        var totalItems = await _context.Items.CountAsync(cancellationToken);

        var lowStockCount = await balanceQuery
            .Include(b => b.Item)
            .CountAsync(b => b.Item != null && b.QuantityOnHand <= b.Item.MinStockAlert, cancellationToken);

        var totalJournalEntries = await journalQuery.CountAsync(cancellationToken);

        var balances = await balanceQuery.ToListAsync(cancellationToken);
        var totalInventoryValue = balances.Sum(b => b.InventoryValue);

        var customerDebt = await scopedInvoices.SumAsync(i => i.RemainingDebt, cancellationToken);

        var topSellingQuery = _context.SalesInvoiceItems
            .Include(it => it.SalesInvoice)
            .Include(it => it.Item)
            .Where(it => it.SalesInvoice!.Status == VoucherStatus.Posted)
            .AsNoTracking();

        if (targetBranchIds != null && targetBranchIds.Count > 0)
        {
            topSellingQuery = topSellingQuery.Where(it => it.SalesInvoice != null && targetBranchIds.Contains(it.SalesInvoice.BranchId));
        }

        var topSelling = await topSellingQuery
            .GroupBy(it => new { it.ItemId, Code = it.Item != null ? it.Item.Code : "---", Name = it.Item != null ? it.Item.Name : "---" })
            .Select(g => new TopSellingItemDto
            {
                ItemCode = g.Key.Code,
                ItemName = g.Key.Name,
                QuantitySold = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.Amount)
            })
            .OrderByDescending(x => x.Revenue)
            .Take(5)
            .ToListAsync(cancellationToken);

        return new DashboardMetricsDto
        {
            TodaySales = todaySales,
            TodayInvoiceCount = todayCount,
            TotalItems = totalItems,
            LowStockCount = lowStockCount,
            TotalJournalEntries = totalJournalEntries,
            TotalInventoryValue = totalInventoryValue,
            TotalCustomerDebt = customerDebt,
            TopSellingItems = topSelling
        };
    }
}
