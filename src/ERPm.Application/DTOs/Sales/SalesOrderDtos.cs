namespace ERPm.Application.DTOs.Sales;

public class SalesOrderItemDto
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? UnitName { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
}

public class SalesOrderDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public int BranchId { get; set; }
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;

    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public List<SalesOrderItemDto> Items { get; set; } = new();
}

public class CreateSalesOrderItemRequest
{
    public int ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? UnitName { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; } = 0m;
    public decimal LineTotal => (Quantity * UnitPrice) - DiscountAmount;
}

public class CreateSalesOrderRequest
{
    public string? OrderNumber { get; set; }
    public DateTime? OrderDate { get; set; }
    public int BranchId { get; set; }
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = "Khách lẻ";

    public decimal DiscountAmount { get; set; } = 0m;
    public decimal TaxAmount { get; set; } = 0m;
    public decimal PaidAmount { get; set; } = 0m;
    public string? Notes { get; set; }

    public List<CreateSalesOrderItemRequest> Items { get; set; } = new();
}

public class UpdateSalesOrderRequest : CreateSalesOrderRequest
{
    public string? Status { get; set; }
}
