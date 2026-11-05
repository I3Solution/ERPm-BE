using ERPm.API.Security;
using ERPm.Application.Common;
using ERPm.Application.Common.Crud;
using ERPm.Application.DTOs.Sales;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers.SALES;

/// <summary>
/// Sales dashboards (SD module). Figures cover the period [from, to) in UTC, default the current month so far,
/// and only the records the caller's data scope allows.
/// </summary>
[PermissionResource("sd:report")]
[Route("api/v1/sales/analytics")]
public class SalesAnalyticsController : BaseApiController
{
    private readonly ISalesAnalyticsService _analytics;

    public SalesAnalyticsController(ISalesAnalyticsService analytics)
    {
        _analytics = analytics;
    }

    /// <summary>Revenue, cost, gross profit and order counts.</summary>
    [RequirePermission(":view")]
    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponse<SalesSummaryDto>>> Summary([FromQuery] SalesAnalyticsQuery query, CancellationToken cancellationToken) =>
        Ok(ApiResponse<SalesSummaryDto>.Ok(await _analytics.GetSummaryAsync(query, cancellationToken)));

    /// <summary>Orders and revenue per salesperson, best first.</summary>
    [RequirePermission(":view")]
    [HttpGet("salesperson-kpi")]
    public async Task<ActionResult<ApiResponse<List<SalespersonKpiDto>>>> SalespersonKpi([FromQuery] SalesAnalyticsQuery query, CancellationToken cancellationToken) =>
        Ok(ApiResponse<List<SalespersonKpiDto>>.Ok(await _analytics.GetSalespersonKpiAsync(query, cancellationToken)));

    /// <summary>Quotations, orders and invoices with the quotation conversion rate.</summary>
    [RequirePermission(":view")]
    [HttpGet("funnel")]
    public async Task<ActionResult<ApiResponse<SalesFunnelDto>>> Funnel([FromQuery] SalesAnalyticsQuery query, CancellationToken cancellationToken) =>
        Ok(ApiResponse<SalesFunnelDto>.Ok(await _analytics.GetFunnelAsync(query, cancellationToken)));
}
