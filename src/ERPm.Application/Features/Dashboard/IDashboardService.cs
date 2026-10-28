using ERPm.Application.DTOs.Dashboard;

namespace ERPm.Application.Features.Dashboard;

public interface IDashboardService
{
    Task<DashboardMetricsDto> GetMetricsAsync(Guid? branchId = null, CancellationToken cancellationToken = default);
}
