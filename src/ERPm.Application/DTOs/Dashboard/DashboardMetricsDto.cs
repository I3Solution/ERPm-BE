namespace ERPm.Application.DTOs.Dashboard;

public class DashboardMetricsDto
{
    public decimal TodaySales { get; set; }
    public int TodayInvoiceCount { get; set; }
    public int TotalItems { get; set; }
    public int LowStockCount { get; set; }
    public int TotalJournalEntries { get; set; }
    public decimal TotalInventoryValue { get; set; }
    public decimal TotalCustomerDebt { get; set; }
    public List<TopSellingItemDto> TopSellingItems { get; set; } = new();
}

public class TopSellingItemDto
{
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public decimal QuantitySold { get; set; }
    public decimal Revenue { get; set; }
}
