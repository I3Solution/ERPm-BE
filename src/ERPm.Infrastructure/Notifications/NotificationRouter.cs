using ERPm.Application.Common.Notifications;
using ERPm.Application.Features.System;
using ERPm.Domain.Enums;
using ERPm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ERPm.Infrastructure.Notifications;

/// <summary>
/// Evaluates organizational structure (branches, departments, job positions)
/// and IAM roles to determine the exact list of recipient user IDs.
/// </summary>
public class NotificationRouter : INotificationRouter
{
    private readonly ApplicationDbContext _context;
    private readonly IBranchService _branchService;

    public NotificationRouter(ApplicationDbContext context, IBranchService branchService)
    {
        _context = context;
        _branchService = branchService;
    }

    public async Task<IReadOnlyList<int>> ResolveRecipientUserIdsAsync(
        string targetType,
        int? targetId,
        int? branchId,
        int? companyId,
        CancellationToken cancellationToken = default)
    {
        var normalizedType = targetType.ToUpperInvariant();

        return normalizedType switch
        {
            NotificationTargetType.User => await ResolveUserAsync(targetId, cancellationToken),
            NotificationTargetType.JobPosition => await ResolveJobPositionAsync(targetId, branchId, companyId, cancellationToken),
            NotificationTargetType.Department => await ResolveDepartmentAsync(targetId, branchId, companyId, cancellationToken),
            NotificationTargetType.BranchTree => await ResolveBranchTreeAsync(targetId ?? branchId, cancellationToken),
            NotificationTargetType.Company => await ResolveCompanyAsync(targetId ?? companyId, cancellationToken),
            NotificationTargetType.Role => await ResolveRoleAsync(targetId, cancellationToken),
            NotificationTargetType.All => await ResolveAllActiveUsersAsync(cancellationToken),
            _ => await ResolveAllActiveUsersAsync(cancellationToken)
        };
    }

    private async Task<IReadOnlyList<int>> ResolveUserAsync(int? userId, CancellationToken cancellationToken)
    {
        if (!userId.HasValue) return [];

        var exists = await _context.AppUsers
            .AsNoTracking()
            .AnyAsync(u => u.Id == userId.Value && u.IsActive && !u.IsDeleted, cancellationToken);

        return exists ? [userId.Value] : [];
    }

    private async Task<IReadOnlyList<int>> ResolveJobPositionAsync(int? positionId, int? branchId, int? companyId, CancellationToken cancellationToken)
    {
        if (!positionId.HasValue) return [];

        var query = _context.Employees
            .AsNoTracking()
            .Where(e => e.JobPositionId == positionId.Value && e.IsActive && !e.IsDeleted && e.UserId != null);

        if (branchId.HasValue)
            query = query.Where(e => e.BranchId == branchId.Value);

        if (companyId.HasValue)
            query = query.Where(e => e.CompanyId == companyId.Value);

        return await query.Select(e => e.UserId!.Value).Distinct().ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<int>> ResolveDepartmentAsync(int? departmentId, int? branchId, int? companyId, CancellationToken cancellationToken)
    {
        if (!departmentId.HasValue) return [];

        var query = _context.Employees
            .AsNoTracking()
            .Where(e => e.DepartmentId == departmentId.Value && e.IsActive && !e.IsDeleted && e.UserId != null);

        if (branchId.HasValue)
            query = query.Where(e => e.BranchId == branchId.Value);

        if (companyId.HasValue)
            query = query.Where(e => e.CompanyId == companyId.Value);

        return await query.Select(e => e.UserId!.Value).Distinct().ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<int>> ResolveBranchTreeAsync(int? rootBranchId, CancellationToken cancellationToken)
    {
        if (!rootBranchId.HasValue) return await ResolveAllActiveUsersAsync(cancellationToken);

        var branchIds = await _branchService.GetBranchAndDescendantIdsAsync(rootBranchId.Value, cancellationToken);
        if (branchIds.Count == 0) branchIds = [rootBranchId.Value];

        var employeeUserIds = await _context.Employees
            .AsNoTracking()
            .Where(e => branchIds.Contains(e.BranchId) && e.IsActive && !e.IsDeleted && e.UserId != null)
            .Select(e => e.UserId!.Value)
            .ToListAsync(cancellationToken);

        // Also include users whose assigned BranchIds overlap
        var allUsers = await _context.AppUsers
            .AsNoTracking()
            .Where(u => u.IsActive && !u.IsDeleted)
            .Select(u => new { u.Id, u.BranchIds })
            .ToListAsync(cancellationToken);

        var directBranchUsers = allUsers
            .Where(u => u.BranchIds != null && u.BranchIds.Any(b => branchIds.Contains(b)))
            .Select(u => u.Id);

        return employeeUserIds.Concat(directBranchUsers).Distinct().ToList();
    }

    private async Task<IReadOnlyList<int>> ResolveCompanyAsync(int? companyId, CancellationToken cancellationToken)
    {
        if (!companyId.HasValue) return await ResolveAllActiveUsersAsync(cancellationToken);

        return await ResolveBranchTreeAsync(companyId.Value, cancellationToken);
    }

    private async Task<IReadOnlyList<int>> ResolveRoleAsync(int? roleId, CancellationToken cancellationToken)
    {
        if (!roleId.HasValue) return [];

        var directUsers = await _context.UserRoles
            .AsNoTracking()
            .Where(ur => ur.RoleId == roleId.Value)
            .Select(ur => ur.UserId)
            .ToListAsync(cancellationToken);

        var positionIds = await _context.JobPositionRoles
            .AsNoTracking()
            .Where(jpr => jpr.RoleId == roleId.Value)
            .Select(jpr => jpr.JobPositionId)
            .ToListAsync(cancellationToken);

        var positionUsers = await _context.Employees
            .AsNoTracking()
            .Where(e => positionIds.Contains(e.JobPositionId) && e.IsActive && !e.IsDeleted && e.UserId != null)
            .Select(e => e.UserId!.Value)
            .ToListAsync(cancellationToken);

        return directUsers.Concat(positionUsers).Distinct().ToList();
    }

    private async Task<IReadOnlyList<int>> ResolveAllActiveUsersAsync(CancellationToken cancellationToken)
    {
        return await _context.AppUsers
            .AsNoTracking()
            .Where(u => u.IsActive && !u.IsDeleted)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
    }
}
