namespace ERPm.Application.DTOs.Sales;

public class SalesReturnItemDto
{
    public int? Id { get; set; }
    public int? InvoiceItemId { get; set; }
    public int ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? UnitName { get; set; }
    public decimal ReturnQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal RefundAmount { get; set; }
    public string? Reason { get; set; }
}

/// <summary>Return header; <see cref="Items"/> is filled by the detail endpoint only, the paged list leaves it empty.</summary>
public class SalesReturnDetailDto
{
    public int Id { get; set; }
    public Guid ExternalId { get; set; }
    public string ReturnNumber { get; set; } = string.Empty;
    public DateTime ReturnDate { get; set; }
    public int? InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int BranchId { get; set; }
    public int WarehouseId { get; set; }
    public decimal TotalRefundAmount { get; set; }
    /// <summary>1 = Cash, 2 = BankTransfer, 3 = ReduceDebt.</summary>
    public int RefundMethod { get; set; }
    public string Status { get; set; } = string.Empty;
    public int? StockReceiptId { get; set; }
    public int? JournalEntryId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public List<SalesReturnItemDto> Items { get; set; } = new();
}

public class CreateSalesReturnLineRequest
{
    public int? InvoiceItemId { get; set; }
    public int ItemId { get; set; }
    public decimal Quantity { get; set; }
    /// <summary>Used for a return without invoice only; with an invoice the refund price comes from the invoice line.</summary>
    public decimal UnitPrice { get; set; }
    public string? Reason { get; set; }
}

public class CreateSalesReturnRequest
{
    public int? InvoiceId { get; set; }
    /// <summary>Required without an invoice (and for ReduceDebt); with an invoice it must match the invoice customer.</summary>
    public int? CustomerId { get; set; }
    public int BranchId { get; set; }
    public int WarehouseId { get; set; }
    /// <summary>1 = Cash, 2 = BankTransfer, 3 = ReduceDebt.</summary>
    public int RefundMethod { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public List<CreateSalesReturnLineRequest> Items { get; set; } = new();
}
