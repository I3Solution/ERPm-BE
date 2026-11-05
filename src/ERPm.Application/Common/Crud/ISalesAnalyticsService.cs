using ERPm.Application.DTOs.Sales;

namespace ERPm.Application.Common.Crud;

/// <summary>Read-only sales figures for dashboards. Everything honours the caller's data scope.</summary>
public interface ISalesAnalyticsService
{
    Task<SalesSummaryDto> GetSummaryAsync(SalesAnalyticsQuery query, CancellationToken cancellationToken = default);
    Task<List<SalespersonKpiDto>> GetSalespersonKpiAsync(SalesAnalyticsQuery query, CancellationToken cancellationToken = default);
    Task<SalesFunnelDto> GetFunnelAsync(SalesAnalyticsQuery query, CancellationToken cancellationToken = default);
}
