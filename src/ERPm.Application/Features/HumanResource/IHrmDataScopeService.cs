using ERPm.Domain.Services.HumanResource;

namespace ERPm.Application.Features.HumanResource;

/// <summary>
/// Resolves HRM data permissions of the current account and loads employee work-history timelines,
/// so visibility and edit rights are always decided on the server.
/// </summary>
public interface IHrmDataScopeService
{
    /// <summary>Scope of the current account; Admin and system calls are unrestricted.</summary>
    Task<DataScope> GetCurrentScopeAsync(CancellationToken cancellationToken = default);

    /// <summary>Timeline built from the employee's approved work-history rows.</summary>
    Task<WorkTimeline> GetTimelineAsync(int employeeId, CancellationToken cancellationToken = default);
}
