using ERPm.Application.DTOs.Dashboard;

namespace ERPm.Application.Features.Dashboard;

public interface IDashboardService
{
    Task<DashboardMetricsDto> GetMetricsAsync(int? branchId = null, CancellationToken cancellationToken = default);
}
