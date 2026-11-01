using ERPm.Application.Common;
using ERPm.Application.Common.Interfaces;
using ERPm.Application.DTOs.System;
using ERPm.Application.Features.System;
using ERPm.Domain.Entities.System;
using ERPm.Domain.Services.Iam;
using ERPm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace ERPm.Infrastructure.Services.Iam;

/// <summary>
/// Builds the effective principal of an account: legacy system role + job-position roles + extra user roles
/// + inline policy, with the organization position taken from the current approved work-history row.
/// Principals are cached per user, security stamp and global IAM version.
/// </summary>
public sealed class IamService(ApplicationDbContext context, IMemoryCache cache, ICurrentUser? currentUser = null) : IIamService
{
    private static long _version;

    public void Invalidate() => Interlocked.Increment(ref _version);

    public async Task<IamPrincipal?> GetCurrentPrincipalAsync(CancellationToken cancellationToken = default)
    {
        if (currentUser == null || currentUser.IsSystem || !int.TryParse(currentUser.UserId, out var id)) return null;
        return await GetPrincipalAsync(id, cancellationToken);
    }

    public async Task EnsureCanGrantAsync(IEnumerable<IamStatement> statements, CancellationToken cancellationToken = default)
    {
        var principal = await GetCurrentPrincipalAsync(cancellationToken);
        if (principal == null) return;
        if (principal.Escalations(statements).Count > 0) throw new BusinessException("error.iam.escalation", 403);
    }

    public async Task<IamPrincipal> GetPrincipalAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await context.AppUsers.IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.Id == userId && !u.IsDeleted)
            .Select(u => new { u.Id, u.Role, u.IsSuperAdmin, u.SecurityStamp, u.BranchIds, u.InlinePolicy })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new BusinessException("error.auth.expired", 401);
        var key = $"iam:{user.Id}:{user.SecurityStamp}:{Interlocked.Read(ref _version)}:{DateOnly.FromDateTime(DateTime.UtcNow)}";
        if (cache.TryGetValue(key, out IamPrincipal? cached)) return cached!;

        var position = await CurrentPositionAsync(user.Id, cancellationToken);
        var branches = await context.Branches.IgnoreQueryFilters().AsNoTracking().Where(b => !b.IsDeleted && b.IsActive)
            .Select(b => new { b.Id, b.ParentId }).ToListAsync(cancellationToken);
        var departments = await context.Departments.AsNoTracking().Where(d => d.IsActive)
            .Select(d => new { d.Id, d.ParentId, d.BranchId }).ToListAsync(cancellationToken);
        var org = new OrgContext
        {
            UserKey = user.Id.ToString(),
            CompanyId = position?.CompanyId,
            BranchId = position?.BranchId,
            DepartmentId = position?.DepartmentId,
            DefaultScopeMode = position?.DefaultScopeMode,
            LegacyBranchIds = user.BranchIds,
            Branches = new OrgTree(branches.Select(b => (b.Id, b.ParentId))),
            Departments = new OrgTree(departments.Select(d => (d.Id, d.ParentId))),
            DepartmentBranches = departments.ToDictionary(d => d.Id, d => d.BranchId)
        };

        var labelled = await CollectStatementsAsync(user.Id, user.Role, user.InlinePolicy, position, departments.ToDictionary(d => d.Id, d => d.ParentId), cancellationToken);
        var statements = labelled.Select(l => l.Statement).ToList();

        var principal = new IamPrincipal(statements, org, user.IsSuperAdmin || user.Role == "Admin")
        {
            EmployeeId = position?.EmployeeId,
            JobPositionId = position?.JobPositionId
        };
        cache.Set(key, principal, TimeSpan.FromMinutes(5));
        return principal;
    }

    /// <summary>Statements of one account, each tagged with where it came from (for the effective-access preview).</summary>
    private async Task<List<(IamStatement Statement, string Source)>> CollectStatementsAsync(int userId, string legacyRole, IamPolicyDocument? inlinePolicy,
        Position? position, IReadOnlyDictionary<int, int?> departmentParents, CancellationToken cancellationToken)
    {
        var result = new List<(IamStatement, string)>();
        var legacyCode = legacyRole.ToUpperInvariant();
        var legacy = await context.IamRoles.AsNoTracking()
            .Where(r => r.IsActive && r.IsSystemRole && r.CompanyId == null && r.Code == legacyCode)
            .Select(r => r.PolicyDocument).FirstOrDefaultAsync(cancellationToken);
        if (legacy != null) result.AddRange(legacy.Statements.Select(st => (st, $"account-role:{legacyCode}")));

        // Roles owned by another company never apply, even if assigned by mistake.
        var companyId = position?.CompanyId;
        if (position != null)
            result.AddRange(await PositionStatementsAsync([(position.DepartmentId, position.JobPositionId)], companyId, departmentParents, cancellationToken));
        var userRoles = await (from link in context.UserRoles
                               join role in context.IamRoles on link.RoleId equals role.Id
                               where link.UserId == userId && role.IsActive && (role.CompanyId == null || role.CompanyId == companyId)
                               select new { role.Code, role.PolicyDocument, link.ScopeOverride }).AsNoTracking().ToListAsync(cancellationToken);
        foreach (var grant in userRoles)
            result.AddRange(grant.PolicyDocument.Statements.Select(st => (grant.ScopeOverride == null || IamPolicyEvaluator.IsDeny(st) ? st
                : new IamStatement { Sid = st.Sid, Effect = st.Effect, Actions = st.Actions, Resources = st.Resources, DataScope = grant.ScopeOverride }, $"user-role:{grant.Code}")));
        if (inlinePolicy != null) result.AddRange(inlinePolicy.Statements.Select(st => (st, "inline-policy")));
        return result;
    }

    public async Task<List<EffectivePermissionDto>> GetEffectiveAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await context.AppUsers.IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.Id == userId && !u.IsDeleted)
            .Select(u => new { u.Id, u.Role, u.IsSuperAdmin, u.InlinePolicy })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new BusinessException("error.auth.expired", 404);
        var position = await CurrentPositionAsync(user.Id, cancellationToken);
        var departmentParents = await context.Departments.AsNoTracking().Where(d => d.IsActive)
            .ToDictionaryAsync(d => d.Id, d => d.ParentId, cancellationToken);
        var labelled = await CollectStatementsAsync(user.Id, user.Role, user.InlinePolicy, position, departmentParents, cancellationToken);
        var superAdmin = user.IsSuperAdmin || user.Role == "Admin";

        return PermissionCatalog.All.Select(item =>
        {
            var matching = labelled.Where(l => IamPolicyEvaluator.Applies(l.Statement, item.Action)).ToList();
            var sources = matching.Select(m => new EffectiveSourceDto(m.Source, m.Statement.Sid, m.Statement.Effect)).ToList();
            var denied = matching.Any(m => IamPolicyEvaluator.IsDeny(m.Statement));
            var allowed = matching.Any(m => !IamPolicyEvaluator.IsDeny(m.Statement));
            // A super admin bypasses statements, but an explicit deny is still shown so the preview explains it.
            var status = superAdmin ? EffectiveStatus.Allowed : denied ? EffectiveStatus.Denied : allowed ? EffectiveStatus.Allowed : EffectiveStatus.NotGranted;
            return new EffectivePermissionDto(item.Action, item.Module, item.Resource, item.Verb, status, sources);
        }).ToList();
    }

    /// <summary>
    /// Statements granted by org positions: the job position's roles, the department's own roles and the roles of
    /// ancestor departments marked ApplyToChildren. Takes a list so concurrent assignments can be added later.
    /// </summary>
    private async Task<List<(IamStatement, string)>> PositionStatementsAsync(IReadOnlyCollection<(int? DepartmentId, int? JobPositionId)> positions,
        int? companyId, IReadOnlyDictionary<int, int?> departmentParents, CancellationToken cancellationToken)
    {
        var jobPositionIds = positions.Where(p => p.JobPositionId != null).Select(p => p.JobPositionId!.Value).Distinct().ToArray();
        var ownDepartments = positions.Where(p => p.DepartmentId != null).Select(p => p.DepartmentId!.Value).Distinct().ToArray();
        var ancestors = new HashSet<int>();
        foreach (var departmentId in ownDepartments)
        {
            var cursor = departmentParents.GetValueOrDefault(departmentId);
            while (cursor is { } parent && ancestors.Add(parent)) cursor = departmentParents.GetValueOrDefault(parent);
        }
        var ancestorIds = ancestors.ToArray();

        var positionRoles = await (from link in context.JobPositionRoles
                                   join role in context.IamRoles on link.RoleId equals role.Id
                                   where jobPositionIds.Contains(link.JobPositionId) && role.IsActive && (role.CompanyId == null || role.CompanyId == companyId)
                                   select new { role.Code, role.PolicyDocument }).AsNoTracking().ToListAsync(cancellationToken);
        var departmentRoles = await (from link in context.DepartmentRoles
                                     join role in context.IamRoles on link.RoleId equals role.Id
                                     where (ownDepartments.Contains(link.DepartmentId) || (link.ApplyToChildren && ancestorIds.Contains(link.DepartmentId)))
                                         && role.IsActive && (role.CompanyId == null || role.CompanyId == companyId)
                                     select new { role.Code, role.PolicyDocument }).AsNoTracking().ToListAsync(cancellationToken);
        return positionRoles.Select(r => (r, "job-position"))
            .Concat(departmentRoles.Select(r => (r, "department")))
            .SelectMany(x => x.Item1.PolicyDocument.Statements.Select(st => (st, $"{x.Item2}:{x.Item1.Code}"))).ToList();
    }

    private sealed record Position(int EmployeeId, int CompanyId, int BranchId, int? DepartmentId, int? JobPositionId, string? DefaultScopeMode);

    /// <summary>
    /// Current org position of the employee linked to the account: the latest approved work-history row in
    /// effect today (none after a termination), falling back to the employee cache fields.
    /// </summary>
    private async Task<Position?> CurrentPositionAsync(int userId, CancellationToken cancellationToken)
    {
        var employee = await context.Employees.AsNoTracking().Where(e => e.UserId == userId)
            .Select(e => new { e.Id, e.CompanyId, e.BranchId, e.DepartmentId, e.JobPositionId, e.IsActive })
            .SingleOrDefaultAsync(cancellationToken);
        if (employee == null) return null;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var row = await context.WorkHistories.AsNoTracking()
            .Where(w => w.EmployeeId == employee.Id && w.IsApproved && w.DateEffective <= today)
            .OrderByDescending(w => w.DateEffective).ThenByDescending(w => w.CreatedAtUtc).ThenByDescending(w => w.Id)
            .Select(w => new { w.CompanyId, w.BranchId, w.DepartmentId, w.JobPositionId, w.IsTermination })
            .FirstOrDefaultAsync(cancellationToken);
        if (row?.IsTermination == true || (row == null && !employee.IsActive)) return null;
        var current = row == null
            ? (employee.CompanyId, employee.BranchId, employee.DepartmentId, employee.JobPositionId)
            : (row.CompanyId, row.BranchId, row.DepartmentId, row.JobPositionId);
        var mode = await context.JobPositions.AsNoTracking().Where(p => p.Id == current.JobPositionId && p.IsActive)
            .Select(p => p.DefaultScopeMode).FirstOrDefaultAsync(cancellationToken);
        // An inactive job position keeps the org position but grants neither roles nor a default scope.
        return new Position(employee.Id, current.CompanyId, current.BranchId, current.DepartmentId, mode == null ? null : current.JobPositionId, mode);
    }
}
