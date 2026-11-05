namespace ERPm.Application.DTOs.Sales;

/// <summary>
/// Period and branch of an analytics request. The period is half open, [From, To), in UTC.
/// Without dates it covers the current calendar month so far.
/// </summary>
public class SalesAnalyticsQuery
{
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    /// <summary>Restricts the figures to this branch and its descendants.</summary>
    public int? BranchId { get; set; }
}

public class SalesSummaryDto
{
    /// <summary>Posted invoices minus posted returns of the period.</summary>
    public decimal TotalRevenue { get; set; }
    /// <summary>Cost of the goods on posted invoices minus the cost put back by returns.</summary>
    public decimal TotalCost { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal GrossMarginPercent { get; set; }
    /// <summary>Orders created in the period, whatever their status.</summary>
    public int TotalOrders { get; set; }
    public int CompletedOrders { get; set; }
    public int CancelledOrders { get; set; }
}

public class SalespersonKpiDto
{
    public int SalespersonId { get; set; }
    public string SalespersonName { get; set; } = string.Empty;
    public string? DepartmentName { get; set; }
    /// <summary>Confirmed, Processing and Completed orders of the period.</summary>
    public int TotalOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    /// <summary>No targets are stored yet, so these two stay empty.</summary>
    public decimal? TargetRevenue { get; set; }
    public decimal? CompletionRatePercent { get; set; }
}

public class SalesFunnelDto
{
    public int QuotationCount { get; set; }
    public decimal QuotationTotalAmount { get; set; }
    /// <summary>Confirmed, Processing and Completed orders.</summary>
    public int OrderCount { get; set; }
    public decimal OrderTotalAmount { get; set; }
    public int InvoiceCount { get; set; }
    public decimal InvoiceTotalAmount { get; set; }
    /// <summary>Quotations converted into an order, as a share of the quotations of the period.</summary>
    public decimal ConversionRatePercent { get; set; }
}
