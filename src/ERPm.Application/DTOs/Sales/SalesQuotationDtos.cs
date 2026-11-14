namespace ERPm.Application.DTOs.Sales;

public class SalesQuotationItemDto
{
    public int? Id { get; set; }
    public int ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? UnitName { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
    public string? Notes { get; set; }
}

/// <summary>Quotation header; <see cref="Items"/> is filled by the detail endpoint only, the paged list leaves it empty.</summary>
public class SalesQuotationDetailDto
{
    public int Id { get; set; }
    public Guid ExternalId { get; set; }
    public string QuotationNumber { get; set; } = string.Empty;
    public DateTime QuotationDate { get; set; }
    public DateTime ValidUntil { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }
    public int? SalespersonId { get; set; }
    public string? SalespersonName { get; set; }
    public int BranchId { get; set; }
    public string? BranchName { get; set; }
    public int? DepartmentId { get; set; }
    public int? PriceListId { get; set; }
    public int? DeliveryAddressId { get; set; }
    public string? DeliveryRecipientName { get; set; }
    public string? DeliveryPhone { get; set; }
    public string? DeliveryAddressText { get; set; }
    /// <summary>Draft, Sent, Accepted, Rejected, Converted, or Expired when the validity date has passed.</summary>
    public string Status { get; set; } = string.Empty;
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? TermsAndConditions { get; set; }
    public int? ConvertedSalesOrderId { get; set; }
    public List<SalesQuotationItemDto> Items { get; set; } = new();
}

public class CreateSalesQuotationLineRequest
{
    public int ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public string? Notes { get; set; }
}

public class CreateSalesQuotationRequest
{
    public DateTime QuotationDate { get; set; }
    public DateTime ValidUntil { get; set; }
    public int CustomerId { get; set; }
    public int BranchId { get; set; }
    public int? DepartmentId { get; set; }
    public int? SalespersonId { get; set; }
    public int? PriceListId { get; set; }
    /// <summary>Optional saved delivery address belonging to the selected customer.</summary>
    public int? DeliveryAddressId { get; set; }
    public string? TermsAndConditions { get; set; }
    public List<CreateSalesQuotationLineRequest> Items { get; set; } = new();
}

/// <summary>Same shape as create; the line list replaces the stored lines.</summary>
public class UpdateSalesQuotationRequest : CreateSalesQuotationRequest
{
}

public class ConvertQuotationResponse
{
    public int QuotationId { get; set; }
    public int SalesOrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
}
