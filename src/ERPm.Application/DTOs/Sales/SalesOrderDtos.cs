namespace ERPm.Application.DTOs.Sales;

public class SalesOrderItemDto
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public int? UomId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? UnitName { get; set; }
    public decimal Quantity { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal FixedDiscountAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
    public decimal VatRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal InvoicedQuantity { get; set; }
    public int? AppliedPriceListId { get; set; }
    public string? AppliedPriceListName { get; set; }
    public int? AppliedPriceListVersionId { get; set; }
    public string? AppliedVersionCode { get; set; }
}

public class SalesOrderDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public int BranchId { get; set; }
    public int? WarehouseId { get; set; }                  // Shipping warehouse; may belong to another branch of the company
    public string? WarehouseName { get; set; }
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
    public string? CreditApprovalReason { get; set; }
    public string? CreditApprovedBy { get; set; }
    public DateTime? CreditApprovedAtUtc { get; set; }
    public string? Notes { get; set; }

    public string? BranchName { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string? DeliveryAddress { get; set; }
    public int? DeliveryAddressId { get; set; }
    public string? DeliveryRecipientName { get; set; }
    public string? DeliveryPhone { get; set; }
    public string? DeliveryAddressText { get; set; }
    public int? QuotationId { get; set; }
    public string? QuotationNumber { get; set; }
    public int? SalespersonId { get; set; }
    public string? SalespersonName { get; set; }
    public int? PriceListId { get; set; }
    public string? PriceListName { get; set; }

    public List<SalesOrderItemDto> Items { get; set; } = new();
}

public class CreateSalesOrderItemRequest
{
    public int ItemId { get; set; }
    public int? UomId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? UnitName { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; } = 0m;
    public decimal FixedDiscountAmount { get; set; } = 0m;
    public decimal DiscountAmount { get; set; } = 0m;
    /// <summary>VAT percentage of the line (0 to 100). Entered on the line until items carry their own rate.</summary>
    public decimal VatRate { get; set; } = 0m;
    public int? AppliedPriceListId { get; set; }
    public int? AppliedPriceListVersionId { get; set; }
    public bool IsPriceOverridden { get; set; } = false;
    public decimal LineTotal => (Quantity * UnitPrice) - DiscountAmount;
}

public class CreateSalesOrderRequest
{
    public string? OrderNumber { get; set; }
    public DateTime? OrderDate { get; set; }
    public int BranchId { get; set; }
    /// <summary>Shipping warehouse, one the branch may ship from. Left empty, the branch default is taken on confirmation.</summary>
    public int? WarehouseId { get; set; }
    public int? DepartmentId { get; set; }
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = "Khách lẻ";

    public decimal DiscountAmount { get; set; } = 0m;
    /// <summary>Derived from the line VAT. Send 0 or the sum of the lines; any other value is rejected.</summary>
    public decimal TaxAmount { get; set; } = 0m;
    public decimal PaidAmount { get; set; } = 0m;
    public string? Notes { get; set; }

    /// <summary>Delivery date and time as ISO 8601 in UTC (for example 2026-11-10T08:30:00Z).</summary>
    public DateTime? ExpectedDeliveryDate { get; set; }
    public string? DeliveryAddress { get; set; }
    /// <summary>An address of the customer's list; the order keeps a copy of it.</summary>
    public int? DeliveryAddressId { get; set; }
    public int? QuotationId { get; set; }
    public int? SalespersonId { get; set; }
    public int? PriceListId { get; set; }
    /// <summary>When true (or when PriceListId is specified), the server recalculates non-overridden lines from active price lists.</summary>
    public bool ApplyPriceList { get; set; } = false;

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

/// <summary>Several orders acted on at once with one reason (bulk credit approval or rejection).</summary>
public class BulkOrderActionRequest
{
    public List<int> Ids { get; set; } = [];
    public string Reason { get; set; } = string.Empty;
}

/// <summary>Outcome for one order of a bulk action; a failed order carries the error code of the single action.</summary>
public class BulkOrderActionResultDto
{
    public int Id { get; set; }
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
}

public class SalesOrderStatusAuditDto
{
    public int Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public string? ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public string? Reason { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}

public class CancelOrderRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class CreateInvoiceFromOrderRequest
{
    /// <summary>Optional: the order's own warehouse is used. A different one is rejected.</summary>
    public int WarehouseId { get; set; }
    /// <summary>1 = Cash, 2 = BankTransfer, 3 = Debt.</summary>
    public int PaymentMethod { get; set; }
    public decimal TenderedAmount { get; set; }
    public string? Notes { get; set; }
}
