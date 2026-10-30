using ERPm.Application.DTOs.System;
using ERPm.Domain.Entities.System;
using ERPm.Domain.Services.Iam;

namespace ERPm.Application.Features.System;

/// <summary>Resolves and caches the effective IAM principal of an account.</summary>
public interface IIamService
{
    Task<IamPrincipal> GetPrincipalAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Principal of the signed-in user; null for system (background / seeding) calls.</summary>
    Task<IamPrincipal?> GetCurrentPrincipalAsync(CancellationToken cancellationToken = default);

    /// <summary>Rejects with 403 when the signed-in user could not use <paramref name="statements"/> itself.</summary>
    Task EnsureCanGrantAsync(IEnumerable<IamStatement> statements, CancellationToken cancellationToken = default);

    /// <summary>Drops every cached principal after a role, assignment or organization change.</summary>
    void Invalidate();
}

/// <summary>Dynamic roles and their assignment to job positions and accounts.</summary>
public interface IRoleService
{
    Task<List<RoleDto>> GetRolesAsync(CancellationToken cancellationToken = default);
    Task<RoleDto?> GetRoleAsync(int id, CancellationToken cancellationToken = default);
    Task<RoleDto> CreateRoleAsync(SaveRoleRequest request, CancellationToken cancellationToken = default);
    Task<RoleDto?> UpdateRoleAsync(int id, SaveRoleRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteRoleAsync(int id, CancellationToken cancellationToken = default);
    Task<int[]> GetJobPositionRolesAsync(int jobPositionId, CancellationToken cancellationToken = default);
    Task<int[]> SetJobPositionRolesAsync(int jobPositionId, int[] roleIds, CancellationToken cancellationToken = default);
    Task<UserAccessPolicyDto> GetUserAccessAsync(int userId, CancellationToken cancellationToken = default);
    Task<UserAccessPolicyDto> SetUserAccessAsync(int userId, UserAccessPolicyDto request, CancellationToken cancellationToken = default);
}
