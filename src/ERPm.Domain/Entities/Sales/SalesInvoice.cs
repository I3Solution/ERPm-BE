using ERPm.Domain.Common;
using ERPm.Domain.Entities.Accounting;
using ERPm.Domain.Entities.Catalog;
using ERPm.Domain.Entities.Inventory;
using ERPm.Domain.Entities.System;
using PartnerEntity = ERPm.Domain.Entities.Partner.Partner;

namespace ERPm.Domain.Entities.Sales;

public enum PaymentMethod
{
    Cash = 1,           // Tiền mặt (111)
    BankTransfer = 2,   // Chuyển khoản (112)
    Debt = 3            // Ghi nợ công nợ (131)
}

public class SalesInvoice : BaseAuditableEntity
{
    public string InvoiceNumber { get; set; } = string.Empty; // HD0001
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;

    public Guid BranchId { get; set; }                     // Chi nhánh lập hóa đơn
    public Guid? CustomerId { get; set; }
    public string CustomerName { get; set; } = "Khách lẻ";
    public Guid WarehouseId { get; set; }

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public VoucherStatus Status { get; set; } = VoucherStatus.Posted;

    // Tiền tệ (Bắt buộc decimal(18, 4))
    public decimal SubTotal { get; set; } = 0m;            // Tổng tiền hàng trước chiết khấu
    public decimal DiscountAmount { get; set; } = 0m;      // Chiết khấu hóa đơn
    public decimal TaxAmount { get; set; } = 0m;           // Thuế VAT
    public decimal TotalAmount { get; set; } = 0m;         // Tổng thanh toán
    public decimal PaidAmount { get; set; } = 0m;          // Khách đã trả
    public decimal RemainingDebt { get; set; } = 0m;       // Còn nợ (tính vào 131)

    // Liên kết Kế toán & Xuất kho
    public Guid? JournalEntryId { get; set; }              // Bút toán kép tương ứng
    public Guid? StockIssueId { get; set; }                // Phiếu xuất kho tương ứng
    public string? Notes { get; set; }

    // Navigation
    public virtual Branch? Branch { get; set; }
    public virtual PartnerEntity? Customer { get; set; }
    public virtual Warehouse? Warehouse { get; set; }
    public virtual JournalEntry? JournalEntry { get; set; }
    public virtual StockIssue? StockIssue { get; set; }
    public virtual ICollection<SalesInvoiceItem> Items { get; set; } = new List<SalesInvoiceItem>();
}

public class SalesInvoiceItem : BaseEntity<Guid>
{
    public SalesInvoiceItem()
    {
        Id = Guid.NewGuid();
    }

    public Guid SalesInvoiceId { get; set; }
    public Guid ItemId { get; set; }

    public decimal Quantity { get; set; } = 0m;
    public decimal UnitPrice { get; set; } = 0m;           // Đơn giá bán
    public decimal DiscountPercent { get; set; } = 0m;
    public decimal Amount { get; set; } = 0m;              // Thành tiền sau chiết khấu
    public decimal CostPrice { get; set; } = 0m;           // Giá vốn tại thời điểm bán (để tính COGS)

    // Navigation
    public virtual SalesInvoice? SalesInvoice { get; set; }
    public virtual Item? Item { get; set; }
}
