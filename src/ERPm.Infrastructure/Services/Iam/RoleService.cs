using ERPm.Application.Common;
using ERPm.Application.DTOs.System;
using ERPm.Application.Features.System;
using ERPm.Domain.Entities.System;
using ERPm.Domain.Services.Iam;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace ERPm.Infrastructure.Services.Iam;

/// <summary>
/// Dynamic role management. A company administrator only manages roles, job positions and accounts of its
/// own company; group templates and group-wide positions are reserved to super administrators. Every grant
/// passes the privilege escalation guard.
/// </summary>
public sealed class RoleService(ApplicationDbContext context, IIamService iam, ERPm.Application.Common.Notifications.INotificationEngine? notificationEngine = null) : IRoleService
{
    public async Task<List<RoleDto>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        var granter = await iam.GetCurrentPrincipalAsync(cancellationToken);
        var companyId = granter?.Context.CompanyId;
        var all = IsGroupAdmin(granter);
        var roles = await context.IamRoles.AsNoTracking()
            .Where(r => all || r.CompanyId == null || r.CompanyId == companyId)
            .OrderBy(r => r.CompanyId).ThenBy(r => r.Code).ToListAsync(cancellationToken);
        return roles.Select(ToDto).ToList();
    }

    public async Task<RoleDto?> GetRoleAsync(int id, CancellationToken cancellationToken = default)
    {
        var granter = await iam.GetCurrentPrincipalAsync(cancellationToken);
        var role = await context.IamRoles.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        return role == null || !CanSee(granter, role.CompanyId) ? null : ToDto(role);
    }

    public async Task<RoleDto> CreateRoleAsync(SaveRoleRequest request, CancellationToken cancellationToken = default)
    {
        var granter = await iam.GetCurrentPrincipalAsync(cancellationToken);
        RequireCompany(granter, request.CompanyId);
        await ValidateAsync(null, request, cancellationToken);
        await iam.EnsureCanGrantAsync(request.PolicyDocument.Statements, cancellationToken);
        var role = new IamRole();
        Apply(role, request);
        context.IamRoles.Add(role);
        await context.SaveChangesAsync(cancellationToken);
        iam.Invalidate();
        return ToDto(role);
    }

    public async Task<RoleDto?> UpdateRoleAsync(int id, SaveRoleRequest request, CancellationToken cancellationToken = default)
    {
        var granter = await iam.GetCurrentPrincipalAsync(cancellationToken);
        var role = await context.IamRoles.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (role == null || !CanSee(granter, role.CompanyId)) return null;
        PostingTransaction.Require(!role.IsSystemRole, "error.iam.system_role", 409);
        RequireCompany(granter, role.CompanyId);
        RequireCompany(granter, request.CompanyId);
        await ValidateAsync(id, request, cancellationToken);
        await iam.EnsureCanGrantAsync(request.PolicyDocument.Statements, cancellationToken);
        Apply(role, request);
        await context.SaveChangesAsync(cancellationToken);
        iam.Invalidate();
        return ToDto(role);
    }

    public async Task<bool> DeleteRoleAsync(int id, CancellationToken cancellationToken = default)
    {
        var granter = await iam.GetCurrentPrincipalAsync(cancellationToken);
        var role = await context.IamRoles.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (role == null || !CanSee(granter, role.CompanyId)) return false;
        PostingTransaction.Require(!role.IsSystemRole, "error.iam.system_role", 409);
        RequireCompany(granter, role.CompanyId);
        role.IsDeleted = true;
        role.DeletedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        iam.Invalidate();
        return true;
    }

    public async Task<int[]> GetJobPositionRolesAsync(int jobPositionId, CancellationToken cancellationToken = default)
    {
        PostingTransaction.Require(await context.JobPositions.AnyAsync(p => p.Id == jobPositionId, cancellationToken), "error.validation.reference", 404);
        return await context.JobPositionRoles.Where(l => l.JobPositionId == jobPositionId).Select(l => l.RoleId).OrderBy(id => id).ToArrayAsync(cancellationToken);
    }

    public async Task<int[]> SetJobPositionRolesAsync(int jobPositionId, int[] roleIds, CancellationToken cancellationToken = default)
    {
        var granter = await iam.GetCurrentPrincipalAsync(cancellationToken);
        var position = await context.JobPositions.AsNoTracking().FirstOrDefaultAsync(p => p.Id == jobPositionId, cancellationToken);
        PostingTransaction.Require(position != null, "error.validation.reference", 404);
        RequireCompany(granter, position!.CompanyId);
        var ids = roleIds.Distinct().ToArray();
        var roles = await LoadAssignableRolesAsync(ids, position.CompanyId, cancellationToken);
        var links = await context.JobPositionRoles.Where(l => l.JobPositionId == jobPositionId).ToListAsync(cancellationToken);
        var added = roles.Where(r => links.All(l => l.RoleId != r.Id)).ToList();
        await iam.EnsureCanGrantAsync(added.SelectMany(r => r.PolicyDocument.Statements), cancellationToken);
        context.JobPositionRoles.RemoveRange(links.Where(l => !ids.Contains(l.RoleId)));
        context.JobPositionRoles.AddRange(added.Select(r => new JobPositionRole { JobPositionId = jobPositionId, RoleId = r.Id }));
        await context.SaveChangesAsync(cancellationToken);
        iam.Invalidate();
        return ids.Order().ToArray();
    }

    public async Task<UserAccessPolicyDto> GetUserAccessAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await context.AppUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        PostingTransaction.Require(user != null, "error.validation.reference", 404);
        var grants = await context.UserRoles.AsNoTracking().Where(l => l.UserId == userId).OrderBy(l => l.RoleId)
            .Select(l => new UserRoleGrantDto { RoleId = l.RoleId, ScopeOverride = l.ScopeOverride }).ToListAsync(cancellationToken);
        return new UserAccessPolicyDto { UserId = userId, Roles = grants, InlinePolicy = user!.InlinePolicy };
    }

    public async Task<UserAccessPolicyDto> SetUserAccessAsync(int userId, UserAccessPolicyDto request, CancellationToken cancellationToken = default)
    {
        var granter = await iam.GetCurrentPrincipalAsync(cancellationToken);
        var user = await context.AppUsers.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        PostingTransaction.Require(user != null, "error.validation.reference", 404);
        var companyId = await context.Employees.Where(e => e.UserId == userId).Select(e => (int?)e.CompanyId).FirstOrDefaultAsync(cancellationToken);
        RequireCompany(granter, companyId);
        PostingTransaction.Require(IamPolicyEvaluator.Validate(request.InlinePolicy) == null, "error.iam.policy_invalid");
        var grants = request.Roles.GroupBy(g => g.RoleId).Select(g => g.Last()).ToList();
        foreach (var grant in grants) await ValidateScopeAsync(grant.ScopeOverride, cancellationToken);
        if (request.InlinePolicy != null)
            foreach (var statement in request.InlinePolicy.Statements) await ValidateScopeAsync(statement.DataScope, cancellationToken);
        var roles = (await LoadAssignableRolesAsync(grants.Select(g => g.RoleId).ToArray(), companyId, cancellationToken)).ToDictionary(r => r.Id);

        var links = await context.UserRoles.Where(l => l.UserId == userId).ToListAsync(cancellationToken);
        var granted = new List<IamStatement>();
        foreach (var grant in grants)
        {
            var existing = links.FirstOrDefault(l => l.RoleId == grant.RoleId);
            if (existing != null && SameScope(existing.ScopeOverride, grant.ScopeOverride)) continue;
            granted.AddRange(roles[grant.RoleId].PolicyDocument.Statements.Select(s => grant.ScopeOverride == null ? s
                : new IamStatement { Sid = s.Sid, Effect = s.Effect, Actions = s.Actions, Resources = s.Resources, DataScope = grant.ScopeOverride }));
        }
        if (!SamePolicy(user!.InlinePolicy, request.InlinePolicy) && request.InlinePolicy != null) granted.AddRange(request.InlinePolicy.Statements);
        await iam.EnsureCanGrantAsync(granted, cancellationToken);

        context.UserRoles.RemoveRange(links);
        context.UserRoles.AddRange(grants.Select(g => new UserRole { UserId = userId, RoleId = g.RoleId, ScopeOverride = g.ScopeOverride }));
        user.InlinePolicy = request.InlinePolicy is { Statements.Count: > 0 } ? request.InlinePolicy : null;
        if (notificationEngine != null)
        {
            var roleNames = string.Join(", ", roles.Values.Select(r => r.Name));
            _ = notificationEngine.PublishAsync(new ERPm.Domain.Events.NotificationEvent(
                "SYS_ROLE_CHANGED",
                ERPm.Domain.Enums.NotificationCategory.SYS,
                ERPm.Domain.Enums.NotificationSeverity.INFO,
                ERPm.Domain.Enums.NotificationTargetType.User,
                targetId: userId,
                parameters: new Dictionary<string, object>
                {
                    ["role_name"] = string.IsNullOrEmpty(roleNames) ? "Tùy biến" : roleNames
                },
                actionUrl: "/auth/me"
            ));
        }
        await context.SaveChangesAsync(cancellationToken);
        iam.Invalidate();
        return await GetUserAccessAsync(userId, cancellationToken);
    }

    private static bool IsGroupAdmin(IamPrincipal? granter) => granter == null || granter.IsSuperAdmin;

    private static bool CanSee(IamPrincipal? granter, int? companyId) =>
        IsGroupAdmin(granter) || companyId == null || companyId == granter!.Context.CompanyId;

    /// <summary>Non group administrators may only change objects owned by their own company.</summary>
    private static void RequireCompany(IamPrincipal? granter, int? companyId)
    {
        if (IsGroupAdmin(granter)) return;
        PostingTransaction.Require(companyId != null && companyId == granter!.Context.CompanyId, "error.iam.company_scope", 403);
    }

    /// <summary>Active roles that may be attached to an object of <paramref name="companyId"/>: group templates or that company's own roles.</summary>
    private async Task<List<IamRole>> LoadAssignableRolesAsync(int[] ids, int? companyId, CancellationToken cancellationToken)
    {
        var roles = await context.IamRoles.AsNoTracking().Where(r => ids.Contains(r.Id) && r.IsActive).ToListAsync(cancellationToken);
        PostingTransaction.Require(roles.Count == ids.Length, "error.validation.reference");
        PostingTransaction.Require(roles.All(r => r.CompanyId == null || r.CompanyId == companyId), "error.iam.company_scope", 403);
        return roles;
    }

    private async Task ValidateAsync(int? id, SaveRoleRequest request, CancellationToken cancellationToken)
    {
        request.Code = (request.Code ?? "").Trim().ToUpperInvariant();
        request.Name = (request.Name ?? "").Trim();
        PostingTransaction.Require(request.Code.Length is > 0 and <= 50 && request.Name.Length is > 0 and <= 200 && (request.Description?.Length ?? 0) <= 500, "error.validation.required");
        PostingTransaction.Require(request.PolicyDocument != null && IamPolicyEvaluator.Validate(request.PolicyDocument) == null, "error.iam.policy_invalid");
        foreach (var statement in request.PolicyDocument!.Statements) await ValidateScopeAsync(statement.DataScope, cancellationToken);
        PostingTransaction.Require(request.CompanyId == null || await context.Branches.IgnoreQueryFilters()
            .AnyAsync(b => b.Id == request.CompanyId && !b.IsDeleted && b.OrgUnitType == OrgUnitType.Company, cancellationToken), "error.validation.reference");
        PostingTransaction.Require(!await context.IamRoles.AnyAsync(r => r.Id != id && r.CompanyId == request.CompanyId && r.Code == request.Code, cancellationToken), "error.validation.duplicate", 409);
    }

    private async Task ValidateScopeAsync(IamDataScope? scope, CancellationToken cancellationToken)
    {
        if (scope == null) return;
        PostingTransaction.Require(DataScopeModes.All.Contains(scope.Mode.ToUpperInvariant()), "error.iam.policy_invalid");
        scope.Mode = scope.Mode.ToUpperInvariant();
        var branchIds = scope.CustomBranchIds.Distinct().ToArray();
        var departmentIds = scope.CustomDepartmentIds.Distinct().ToArray();
        PostingTransaction.Require(await context.Branches.IgnoreQueryFilters().CountAsync(b => branchIds.Contains(b.Id) && !b.IsDeleted, cancellationToken) == branchIds.Length
            && await context.Departments.CountAsync(d => departmentIds.Contains(d.Id), cancellationToken) == departmentIds.Length, "error.validation.reference");
    }

    private static bool SameScope(IamDataScope? a, IamDataScope? b) =>
        global::System.Text.Json.JsonSerializer.Serialize(a) == global::System.Text.Json.JsonSerializer.Serialize(b);

    private static bool SamePolicy(IamPolicyDocument? a, IamPolicyDocument? b) =>
        global::System.Text.Json.JsonSerializer.Serialize(a) == global::System.Text.Json.JsonSerializer.Serialize(b);

    private static void Apply(IamRole role, SaveRoleRequest request)
    {
        role.Code = request.Code;
        role.Name = request.Name;
        role.Description = request.Description?.Trim();
        role.Translations = request.Translations ?? new();
        role.CompanyId = request.CompanyId;
        role.PolicyDocument = request.PolicyDocument;
        role.IsActive = request.IsActive;
    }

    private static RoleDto ToDto(IamRole role) => new()
    {
        Id = role.Id,
        Code = role.Code,
        Name = role.Name,
        Description = role.Description,
        Translations = role.Translations,
        CompanyId = role.CompanyId,
        IsSystemRole = role.IsSystemRole,
        PolicyDocument = role.PolicyDocument,
        IsActive = role.IsActive
    };
}
