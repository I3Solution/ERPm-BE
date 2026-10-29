using ERPm.Domain.Entities.Sales;

namespace ERPm.Application.DTOs.Sales;

public class CreateSalesInvoiceRequest
{
    public int? BranchId { get; set; }                     // Originating branch ID
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = "Khách lẻ";
    public int WarehouseId { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public decimal DiscountAmount { get; set; } = 0m;
    public decimal PaidAmount { get; set; } = 0m;
    public string? Notes { get; set; }
    public List<SalesInvoiceItemRequest> Items { get; set; } = new();
}

public class SalesInvoiceItemRequest
{
    public int ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; } = 0m;
}

public class SalesInvoiceDto
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public int BranchId { get; set; }
    public string? BranchCode { get; set; }
    public string? BranchName { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string PaymentMethodName { get; set; } = string.Empty;
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingDebt { get; set; }
    public List<SalesInvoiceItemDto> Items { get; set; } = new();
}

public class SalesInvoiceItemDto
{
    public int ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
}
