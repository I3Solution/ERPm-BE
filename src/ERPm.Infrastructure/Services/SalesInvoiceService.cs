using ERPm.Application.Common.Interfaces;
using ERPm.Application.DTOs.Sales;
using ERPm.Application.Features.Sales;
using ERPm.Application.Features.System;
using ERPm.Domain.Entities.Accounting;
using ERPm.Domain.Entities.Inventory;
using ERPm.Domain.Entities.Sales;
using Microsoft.EntityFrameworkCore;

namespace ERPm.Infrastructure.Services;

public class SalesInvoiceService : ISalesInvoiceService
{
    private readonly IApplicationDbContext _context;
    private readonly IBranchService _branchService;

    public SalesInvoiceService(IApplicationDbContext context, IBranchService branchService)
    {
        _context = context;
        _branchService = branchService;
    }

    public async Task<List<SalesInvoiceDto>> GetInvoicesAsync(int? branchId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.SalesInvoices
            .Include(i => i.Branch)
            .Include(i => i.Items)
                .ThenInclude(it => it.Item)
            .AsNoTracking();

        if (branchId.HasValue && branchId.Value > 0)
        {
            var targetBranchIds = await _branchService.GetBranchAndDescendantIdsAsync(branchId.Value, cancellationToken);
            query = query.Where(i => targetBranchIds.Contains(i.BranchId));
        }

        return await query
            .OrderByDescending(i => i.InvoiceDate)
            .Take(50)
            .Select(i => new SalesInvoiceDto
            {
                Id = i.Id,
                InvoiceNumber = i.InvoiceNumber,
                InvoiceDate = i.InvoiceDate,
                BranchId = i.BranchId,
                BranchCode = i.Branch != null ? i.Branch.Code : null,
                BranchName = i.Branch != null ? i.Branch.Name : null,
                CustomerName = i.CustomerName,
                PaymentMethodName = i.PaymentMethod == PaymentMethod.Cash ? "Tiền mặt" :
                                    i.PaymentMethod == PaymentMethod.BankTransfer ? "Chuyển khoản" : "Ghi nợ",
                SubTotal = i.SubTotal,
                DiscountAmount = i.DiscountAmount,
                TotalAmount = i.TotalAmount,
                PaidAmount = i.PaidAmount,
                RemainingDebt = i.RemainingDebt,
                Items = i.Items.Select(it => new SalesInvoiceItemDto
                {
                    ItemId = it.ItemId,
                    ItemCode = it.Item != null ? it.Item.Code : string.Empty,
                    ItemName = it.Item != null ? it.Item.Name : string.Empty,
                    Quantity = it.Quantity,
                    UnitPrice = it.UnitPrice,
                    Amount = it.Amount
                }).ToList()
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<SalesInvoiceDto> CreateInvoiceAsync(CreateSalesInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var count = await _context.SalesInvoices.CountAsync(cancellationToken);
        var invoiceNumber = $"HD{count + 1:D5}";
        var issueCode = $"PXK{count + 1:D5}";
        var journalCode = $"PKT{count + 1:D5}";

        // Determine the originating branch for the invoice
        var branchId = request.BranchId;
        if (!branchId.HasValue || branchId <= 0)
        {
            var wh = await _context.Warehouses.FirstOrDefaultAsync(w => w.Id == request.WarehouseId, cancellationToken);
            if (wh != null && wh.BranchId > 0)
            {
                branchId = wh.BranchId;
            }
            else
            {
                var rootBranch = await _context.Branches.FirstOrDefaultAsync(b => b.Code == "root" || b.IsDefault, cancellationToken);
                branchId = rootBranch?.Id ?? 1;
            }
        }

        var invoice = new SalesInvoice
        {
            InvoiceNumber = invoiceNumber,
            InvoiceDate = DateTime.UtcNow,
            BranchId = branchId.Value,
            CustomerId = request.CustomerId,
            CustomerName = string.IsNullOrWhiteSpace(request.CustomerName) ? "Khách lẻ" : request.CustomerName,
            WarehouseId = request.WarehouseId,
            PaymentMethod = request.PaymentMethod,
            DiscountAmount = request.DiscountAmount,
            Status = VoucherStatus.Posted,
            Notes = request.Notes
        };

        // 1. Calculate Items & Cost
        decimal subTotal = 0;
        decimal totalCost = 0;

        var stockIssue = new StockIssue
        {
            Code = issueCode,
            IssueDate = DateTime.UtcNow,
            BranchId = branchId.Value,
            WarehouseId = request.WarehouseId,
            CustomerId = request.CustomerId,
            Reason = $"Xuất bán theo hóa đơn {invoiceNumber}",
            Status = VoucherStatus.Posted
        };

        foreach (var itemReq in request.Items)
        {
            var item = await _context.Items.FirstOrDefaultAsync(i => i.Id == itemReq.ItemId, cancellationToken);
            var itemCost = item?.CostPrice ?? 0m;
            var lineAmount = (itemReq.Quantity * itemReq.UnitPrice) * (1 - (itemReq.DiscountPercent / 100m));

            subTotal += lineAmount;
            var lineCost = itemReq.Quantity * itemCost;
            totalCost += lineCost;

            invoice.Items.Add(new SalesInvoiceItem
            {
                ItemId = itemReq.ItemId,
                Quantity = itemReq.Quantity,
                UnitPrice = itemReq.UnitPrice,
                DiscountPercent = itemReq.DiscountPercent,
                Amount = lineAmount,
                CostPrice = itemCost
            });

            stockIssue.Items.Add(new StockIssueItem
            {
                ItemId = itemReq.ItemId,
                Quantity = itemReq.Quantity,
                UnitCost = itemCost,
                TotalCost = lineCost
            });

            // Deduct stock balance
            var balance = await _context.StockBalances
                .FirstOrDefaultAsync(b => b.WarehouseId == request.WarehouseId && b.ItemId == itemReq.ItemId, cancellationToken);

            if (balance != null)
            {
                balance.QuantityOnHand -= itemReq.Quantity;
            }
        }

        invoice.SubTotal = subTotal;
        invoice.TotalAmount = Math.Max(0, subTotal - request.DiscountAmount);
        invoice.PaidAmount = request.PaymentMethod == PaymentMethod.Debt ? 0m : (request.PaidAmount > 0 ? request.PaidAmount : invoice.TotalAmount);
        invoice.RemainingDebt = Math.Max(0, invoice.TotalAmount - invoice.PaidAmount);

        stockIssue.TotalCostAmount = totalCost;
        await _context.StockIssues.AddAsync(stockIssue, cancellationToken);
        invoice.StockIssue = stockIssue;

        // 2. Auto-Generate Double-Entry Journal Entry
        var acc111 = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == "111", cancellationToken);
        var acc112 = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == "112", cancellationToken);
        var acc131 = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == "131", cancellationToken);
        var acc511 = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == "511", cancellationToken);
        var acc632 = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == "632", cancellationToken);
        var acc156 = await _context.Accounts.FirstOrDefaultAsync(a => a.Code == "156", cancellationToken);

        if (acc511 != null)
        {
            var journal = new JournalEntry
            {
                VoucherNumber = journalCode,
                VoucherType = "SALES_INVOICE",
                DocDate = DateTime.UtcNow,
                PostingDate = DateTime.UtcNow,
                BranchId = branchId.Value,
                Description = $"Hạch toán doanh thu & giá vốn hóa đơn {invoiceNumber}",
                Status = VoucherStatus.Posted
            };

            // Revenue entry:
            // Debit 111 / 112 / 131
            var debitAccId = request.PaymentMethod == PaymentMethod.Cash ? (acc111?.Id ?? acc131?.Id) :
                             request.PaymentMethod == PaymentMethod.BankTransfer ? (acc112?.Id ?? acc131?.Id) :
                             acc131?.Id;

            if (debitAccId.HasValue)
            {
                journal.Lines.Add(new JournalEntryLine
                {
                    AccountId = debitAccId.Value,
                    DebitAmount = invoice.TotalAmount,
                    CreditAmount = 0m,
                    Description = $"Thu tiền bán hàng {invoiceNumber}",
                    PartnerId = request.CustomerId
                });

                // Credit 511 (Sales Revenue)
                journal.Lines.Add(new JournalEntryLine
                {
                    AccountId = acc511.Id,
                    DebitAmount = 0m,
                    CreditAmount = invoice.TotalAmount,
                    Description = $"Doanh thu bán hàng {invoiceNumber}",
                    PartnerId = request.CustomerId
                });
            }

            // COGS entry: Debit 632 / Credit 156
            if (acc632 != null && acc156 != null && totalCost > 0)
            {
                journal.Lines.Add(new JournalEntryLine
                {
                    AccountId = acc632.Id,
                    DebitAmount = totalCost,
                    CreditAmount = 0m,
                    Description = $"Giá vốn hàng bán {invoiceNumber}"
                });

                journal.Lines.Add(new JournalEntryLine
                {
                    AccountId = acc156.Id,
                    DebitAmount = 0m,
                    CreditAmount = totalCost,
                    Description = $"Xuất kho hàng hóa {invoiceNumber}"
                });
            }

            await _context.JournalEntries.AddAsync(journal, cancellationToken);
            invoice.JournalEntry = journal;
        }

        // 3. Update Customer Debt if any
        if (request.CustomerId.HasValue && invoice.RemainingDebt > 0)
        {
            var customer = await _context.Partners.FirstOrDefaultAsync(p => p.Id == request.CustomerId.Value, cancellationToken);
            if (customer != null)
            {
                customer.DebtBalance += invoice.RemainingDebt;
            }
        }

        await _context.SalesInvoices.AddAsync(invoice, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return (await GetInvoicesAsync(null, cancellationToken)).First(i => i.Id == invoice.Id);
    }
}
