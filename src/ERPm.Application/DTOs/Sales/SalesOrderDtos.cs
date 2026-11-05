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
    public decimal InvoicedQuantity { get; set; }
}

public class SalesOrderDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public int BranchId { get; set; }
    public int? DepartmentId { get; set; }
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;

    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    /// <summary>Passed, Warning, PendingApproval or Overridden.</summary>
    public string CreditCheckStatus { get; set; } = "Passed";
    /// <summary>None, Reserved or Released.</summary>
    public string ReservationStatus { get; set; } = "None";
    public string? Notes { get; set; }

    public string? BranchName { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string? DeliveryAddress { get; set; }
    public int? QuotationId { get; set; }
    public string? QuotationNumber { get; set; }
    public int? SalespersonId { get; set; }
    public string? SalespersonName { get; set; }
    public int? PriceListId { get; set; }

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
    public int? DepartmentId { get; set; }
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = "Khách lẻ";

    public decimal DiscountAmount { get; set; } = 0m;
    public decimal TaxAmount { get; set; } = 0m;
    public decimal PaidAmount { get; set; } = 0m;
    public string? Notes { get; set; }

    public DateTime? ExpectedDeliveryDate { get; set; }
    public string? DeliveryAddress { get; set; }
    public int? QuotationId { get; set; }
    public int? SalespersonId { get; set; }

    public List<CreateSalesOrderItemRequest> Items { get; set; } = new();
}

public class UpdateSalesOrderRequest : CreateSalesOrderRequest
{
    public string? Status { get; set; }
}

public class ApproveCreditRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class CancelOrderRequest
{
    public string Reason { get; set; } = string.Empty;
}
