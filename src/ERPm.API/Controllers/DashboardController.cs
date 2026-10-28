using ERPm.Application.Common;
using ERPm.Application.DTOs.Dashboard;
using ERPm.Application.Features.Dashboard;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers;

public class DashboardController : BaseApiController
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("metrics")]
    public async Task<ActionResult<ApiResponse<DashboardMetricsDto>>> GetMetrics([FromQuery] Guid? branchId, CancellationToken cancellationToken)
    {
        var result = await _dashboardService.GetMetricsAsync(branchId, cancellationToken);
        return Ok(ApiResponse<DashboardMetricsDto>.Ok(result));
    }
}
