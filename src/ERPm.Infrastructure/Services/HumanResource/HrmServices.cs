using ERPm.Application.Common.Crud;
using ERPm.Application.Common.Interfaces;
using ERPm.Application.DTOs.HumanResource;
using ERPm.Application.Features.System;
using ERPm.Domain.Entities.HumanResource;
using ERPm.Domain.Entities.System;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace ERPm.Infrastructure.Services.HumanResource;

/// <summary>Data scope of the current request applied to HRM rows (branch, department or own profile).</summary>
internal sealed class HrmScope(ICurrentUser? user)
{
    public bool All => user == null || user.IsSystem || user.IsAdmin || user.HasUnrestrictedScope;
    public int[] Branches => user?.BranchIds ?? [];
    public int[] Departments => user?.DepartmentIds ?? [];
    public int? OwnUserId => user is { IncludeOwnRecords: true } && int.TryParse(user.UserId, out var id) ? id : null;

    public bool Covers(int branchId, int? departmentId) =>
        All || Branches.Contains(branchId) || (departmentId is { } d && Departments.Contains(d));

    public void Require(int branchId, int? departmentId) =>
        PostingTransaction.Require(Covers(branchId, departmentId), "error.access.denied", 403);
}

/// <summary>Shared org-tree checks: company node, branch inside the company, department inside the company.</summary>
internal static class OrgRules
{
    public static async Task RequirePlacementAsync(ApplicationDbContext context, int companyId, int branchId, int? departmentId, int? jobPositionId, CancellationToken cancellationToken)
    {
        var branches = context.Branches.IgnoreQueryFilters().Where(b => !b.IsDeleted && b.IsActive);
        PostingTransaction.Require(await branches.AnyAsync(b => b.Id == companyId && b.OrgUnitType == OrgUnitType.Company, cancellationToken), "error.validation.reference");
        PostingTransaction.Require(await branches.AnyAsync(b => b.Id == branchId && (b.Id == companyId || b.CompanyId == companyId), cancellationToken), "error.inventory.branch_mismatch");
        PostingTransaction.Require(departmentId == null || await context.Departments.AnyAsync(d => d.Id == departmentId && d.CompanyId == companyId && d.IsActive, cancellationToken), "error.validation.reference");
        PostingTransaction.Require(jobPositionId == null || await context.JobPositions.AnyAsync(p => p.Id == jobPositionId && p.IsActive && (p.CompanyId == null || p.CompanyId == companyId), cancellationToken), "error.validation.reference");
    }
}

/// <summary>
/// Departments of a company branch. Enforces level order (a child sits on a deeper level than its parent),
/// same company as the parent and no cycles, and maintains the materialized <c>Path</c> / <c>Depth</c>
/// of the whole subtree when a department moves.
/// </summary>
public class DepartmentService(ApplicationDbContext context, IIamService iam, ICurrentUser? user = null)
    : BaseCrudService<Department, DepartmentDto, CreateDepartmentRequest, UpdateDepartmentRequest>(context)
{
    private readonly HrmScope _scope = new(user);

    protected override IQueryable<Department> PrepareQuery(IQueryable<Department> query)
    {
        if (_scope.All) return query;
        var branches = _scope.Branches; var departments = _scope.Departments;
        return query.Where(d => branches.Contains(d.BranchId) || departments.Contains(d.Id));
    }

    protected override IQueryable<Department> ApplyFilters(IQueryable<Department> query, BaseFilterQuery filter)
    {
        query = base.ApplyFilters(query, filter);
        if (filter is not DepartmentFilterQuery departmentFilter) return query;
        if (departmentFilter.CompanyId is { } companyId) query = query.Where(d => d.CompanyId == companyId);
        if (!string.IsNullOrWhiteSpace(departmentFilter.LevelCode))
        {
            var code = departmentFilter.LevelCode.Trim().ToUpperInvariant();
            query = query.Where(d => d.Level!.Code == code);
        }
        if (departmentFilter.UnderId is { } underId)
        {
            var departments = Context.Departments;
            query = query.Where(d => departments.Any(u => u.Id == underId && d.Path.StartsWith(u.Path)));
        }
        return query;
    }

    protected override async Task<Department?> FindEntityAsync(int id, CancellationToken cancellationToken) =>
        await PrepareQuery(DbSet).FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    /// <summary>Insert and Path back-fill (which needs the new id) run in one transaction.</summary>
    public override Task<DepartmentDto> CreateAsync(CreateDepartmentRequest dto, CancellationToken cancellationToken = default) =>
        new PostingTransaction(Context).RunAsync(() => base.CreateAsync(dto, cancellationToken), cancellationToken);

    protected override async Task BeforeCreateAsync(Department entity, CreateDepartmentRequest dto, CancellationToken cancellationToken)
    {
        await ValidateAsync(null, dto, cancellationToken);
        entity.Code = dto.Code;
        entity.Name = dto.Name;
        entity.LevelId = dto.LevelId!.Value;
    }

    protected override async Task AfterCreateAsync(Department entity, CreateDepartmentRequest dto, CancellationToken cancellationToken)
    {
        var parentPath = entity.ParentId is { } parentId
            ? await Context.Departments.Where(d => d.Id == parentId).Select(d => d.Path).FirstAsync(cancellationToken)
            : "/";
        entity.Path = $"{parentPath}{entity.Id}/";
        entity.Depth = PathDepth(entity.Path);
        await Context.SaveChangesAsync(cancellationToken);
        iam.Invalidate();
    }

    protected override async Task BeforeUpdateAsync(Department entity, UpdateDepartmentRequest dto, CancellationToken cancellationToken)
    {
        await ValidateAsync(entity.Id, dto, cancellationToken);
        if (dto.ParentId == entity.ParentId) return;

        // Re-root the whole subtree; saved together with the department itself by the base update.
        var oldPath = entity.Path;
        var parentPath = dto.ParentId is { } parentId
            ? await Context.Departments.Where(d => d.Id == parentId).Select(d => d.Path).FirstAsync(cancellationToken)
            : "/";
        var newPath = $"{parentPath}{entity.Id}/";
        var descendants = await Context.Departments.Where(d => d.Id != entity.Id && d.Path.StartsWith(oldPath)).ToListAsync(cancellationToken);
        foreach (var child in descendants)
        {
            child.Path = newPath + child.Path[oldPath.Length..];
            child.Depth = PathDepth(child.Path);
        }
        entity.Path = newPath;
        entity.Depth = PathDepth(newPath);
    }

    protected override async Task BeforeDeleteAsync(Department entity, CancellationToken cancellationToken)
    {
        var inUse = await Context.Departments.AnyAsync(d => d.ParentId == entity.Id, cancellationToken)
            || await Context.Employees.AnyAsync(e => e.DepartmentId == entity.Id, cancellationToken)
            || await Context.JobPositions.AnyAsync(p => p.DepartmentId == entity.Id, cancellationToken);
        PostingTransaction.Require(!inUse, "error.item.in_use", 409);
    }

    protected override Task AfterUpdateAsync(Department entity, UpdateDepartmentRequest dto, CancellationToken cancellationToken) { iam.Invalidate(); return Task.CompletedTask; }
    protected override Task AfterDeleteAsync(Department entity, CancellationToken cancellationToken) { iam.Invalidate(); return Task.CompletedTask; }

    private static int PathDepth(string path) => path.Count(c => c == '/') - 1;

    private async Task ValidateAsync(int? id, CreateDepartmentRequest dto, CancellationToken cancellationToken)
    {
        dto.Code = (dto.Code ?? "").Trim().ToUpperInvariant();
        dto.Name = (dto.Name ?? "").Trim();
        dto.Translations ??= new();
        PostingTransaction.Require(dto.Code.Length is > 0 and <= 50 && dto.Name.Length is > 0 and <= 200, "error.validation.required");
        await OrgRules.RequirePlacementAsync(Context, dto.CompanyId, dto.BranchId, null, null, cancellationToken);
        _scope.Require(dto.BranchId, id);
        PostingTransaction.Require(!await Context.Departments.AnyAsync(d => d.Id != id && d.CompanyId == dto.CompanyId && d.Code == dto.Code, cancellationToken), "error.validation.duplicate", 409);

        // Level: a group level or one of the company's own levels; null falls back to the group DEPARTMENT level.
        dto.LevelId ??= await Context.DepartmentLevels.Where(l => l.CompanyId == null && l.Code == DepartmentLevel.DepartmentCode)
            .Select(l => (int?)l.Id).FirstOrDefaultAsync(cancellationToken);
        var level = await Context.DepartmentLevels.AsNoTracking()
            .Where(l => l.Id == dto.LevelId && l.IsActive && (l.CompanyId == null || l.CompanyId == dto.CompanyId))
            .Select(l => new { l.SortOrder }).FirstOrDefaultAsync(cancellationToken);
        PostingTransaction.Require(level != null, "error.validation.reference");
        if (id is { } self)
        {
            var childLevels = await Context.Departments.Where(d => d.ParentId == self).Select(d => d.Level!.SortOrder).ToListAsync(cancellationToken);
            PostingTransaction.Require(childLevels.All(order => order > level!.SortOrder), "error.hrm.department_level_order");
        }
        if (dto.ParentId is not { } parentId) return;

        var parents = await Context.Departments.AsNoTracking()
            .Select(d => new { d.Id, d.ParentId, d.CompanyId, LevelOrder = d.Level!.SortOrder }).ToDictionaryAsync(d => d.Id, cancellationToken);
        PostingTransaction.Require(parents.TryGetValue(parentId, out var parent) && parent.CompanyId == dto.CompanyId, "error.validation.reference");
        PostingTransaction.Require(level!.SortOrder > parent!.LevelOrder, "error.hrm.department_level_order");
        // Walk up from the new parent; reaching the edited department would close a cycle.
        int? cursor = parentId;
        var steps = 0;
        while (cursor is { } current)
        {
            PostingTransaction.Require(current != id && steps++ <= parents.Count, "error.validation.reference");
            cursor = parents.TryGetValue(current, out var node) ? node.ParentId : null;
        }
    }
}

/// <summary>
/// Department level catalog. Group levels (no company) need an unrestricted scope; levels in use by a
/// department cannot be deleted.
/// </summary>
public class DepartmentLevelService(ApplicationDbContext context, ICurrentUser? user = null)
    : BaseCrudService<DepartmentLevel, DepartmentLevelDto, CreateDepartmentLevelRequest, UpdateDepartmentLevelRequest>(context)
{
    private readonly HrmScope _scope = new(user);

    /// <summary>Group-wide levels stay visible; a company's levels only to callers whose scope covers that company.</summary>
    protected override IQueryable<DepartmentLevel> PrepareQuery(IQueryable<DepartmentLevel> query)
    {
        if (_scope.All) return query;
        var branches = _scope.Branches;
        return query.Where(l => l.CompanyId == null || branches.Contains(l.CompanyId.Value));
    }

    protected override async Task<DepartmentLevel?> FindEntityAsync(int id, CancellationToken cancellationToken) =>
        await PrepareQuery(DbSet).FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    protected override async Task BeforeCreateAsync(DepartmentLevel entity, CreateDepartmentLevelRequest dto, CancellationToken cancellationToken)
    {
        await ValidateAsync(null, dto, cancellationToken);
        entity.Code = dto.Code;
        entity.Name = dto.Name;
        entity.StandardLevelCode = dto.StandardLevelCode;
    }

    protected override async Task BeforeUpdateAsync(DepartmentLevel entity, UpdateDepartmentLevelRequest dto, CancellationToken cancellationToken)
    {
        RequireOwner(entity.CompanyId);
        await ValidateAsync(entity.Id, dto, cancellationToken);
        if (dto.SortOrder == entity.SortOrder) return;
        // A new order must keep every existing parent/child pair valid.
        var id = entity.Id;
        var broken = await Context.Departments.AnyAsync(d => d.ParentId != null
            && ((d.LevelId == id && d.Parent!.Level!.SortOrder >= dto.SortOrder) || (d.Parent!.LevelId == id && d.Level!.SortOrder <= dto.SortOrder)), cancellationToken);
        PostingTransaction.Require(!broken, "error.hrm.department_level_order");
    }

    protected override async Task BeforeDeleteAsync(DepartmentLevel entity, CancellationToken cancellationToken)
    {
        RequireOwner(entity.CompanyId);
        PostingTransaction.Require(!await Context.Departments.AnyAsync(d => d.LevelId == entity.Id, cancellationToken), "error.validation.reference", 409);
    }

    private void RequireOwner(int? companyId) =>
        PostingTransaction.Require(_scope.All || (companyId is { } c && _scope.Branches.Contains(c)), "error.access.denied", 403);

    private async Task ValidateAsync(int? id, CreateDepartmentLevelRequest dto, CancellationToken cancellationToken)
    {
        dto.Code = (dto.Code ?? "").Trim().ToUpperInvariant();
        dto.Name = (dto.Name ?? "").Trim();
        dto.StandardLevelCode = string.IsNullOrWhiteSpace(dto.StandardLevelCode) ? null : dto.StandardLevelCode.Trim().ToUpperInvariant();
        dto.Translations ??= new();
        PostingTransaction.Require(dto.Code.Length is > 0 and <= 50 && dto.Name.Length is > 0 and <= 200 && (dto.StandardLevelCode?.Length ?? 0) <= 50, "error.validation.required");
        PostingTransaction.Require(dto.SortOrder > 0, "error.validation.request");
        RequireOwner(dto.CompanyId);
        PostingTransaction.Require(dto.CompanyId == null || await Context.Branches.IgnoreQueryFilters()
            .AnyAsync(b => b.Id == dto.CompanyId && !b.IsDeleted && b.OrgUnitType == OrgUnitType.Company, cancellationToken), "error.validation.reference");
        PostingTransaction.Require(!await Context.DepartmentLevels.AnyAsync(l => l.Id != id && l.CompanyId == dto.CompanyId && l.Code == dto.Code, cancellationToken), "error.validation.duplicate", 409);
    }
}

/// <summary>
/// Job positions. Group-wide positions (no company) need an unrestricted scope; company positions need the
/// company inside the caller's scope.
/// </summary>
public class JobPositionService(ApplicationDbContext context, IIamService iam, ICurrentUser? user = null)
    : BaseCrudService<JobPosition, JobPositionDto, CreateJobPositionRequest, UpdateJobPositionRequest>(context)
{
    private readonly HrmScope _scope = new(user);

    /// <summary>Group-wide positions stay visible; a company's positions only to callers whose scope covers that company.</summary>
    protected override IQueryable<JobPosition> PrepareQuery(IQueryable<JobPosition> query)
    {
        if (_scope.All) return query;
        var branches = _scope.Branches;
        return query.Where(p => p.CompanyId == null || branches.Contains(p.CompanyId.Value));
    }

    protected override async Task<JobPosition?> FindEntityAsync(int id, CancellationToken cancellationToken) =>
        await PrepareQuery(DbSet).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    protected override async Task BeforeCreateAsync(JobPosition entity, CreateJobPositionRequest dto, CancellationToken cancellationToken)
    {
        await ValidateAsync(null, dto, cancellationToken);
        entity.Code = dto.Code;
        entity.Name = dto.Name;
        entity.DefaultScopeMode = dto.DefaultScopeMode;
    }

    protected override async Task BeforeUpdateAsync(JobPosition entity, UpdateJobPositionRequest dto, CancellationToken cancellationToken)
    {
        RequireOwner(entity.CompanyId);
        await ValidateAsync(entity.Id, dto, cancellationToken);
    }

    protected override async Task BeforeDeleteAsync(JobPosition entity, CancellationToken cancellationToken)
    {
        RequireOwner(entity.CompanyId);
        PostingTransaction.Require(!await Context.Employees.AnyAsync(e => e.JobPositionId == entity.Id, cancellationToken)
            && !await Context.WorkHistories.AnyAsync(w => w.JobPositionId == entity.Id, cancellationToken), "error.item.in_use", 409);
    }

    protected override Task AfterUpdateAsync(JobPosition entity, UpdateJobPositionRequest dto, CancellationToken cancellationToken) { iam.Invalidate(); return Task.CompletedTask; }
    protected override Task AfterDeleteAsync(JobPosition entity, CancellationToken cancellationToken) { iam.Invalidate(); return Task.CompletedTask; }

    private void RequireOwner(int? companyId) =>
        PostingTransaction.Require(_scope.All || (companyId is { } c && _scope.Branches.Contains(c)), "error.access.denied", 403);

    private async Task ValidateAsync(int? id, CreateJobPositionRequest dto, CancellationToken cancellationToken)
    {
        dto.Code = (dto.Code ?? "").Trim().ToUpperInvariant();
        dto.Name = (dto.Name ?? "").Trim();
        dto.DefaultScopeMode = (dto.DefaultScopeMode ?? "").Trim().ToUpperInvariant();
        dto.Translations ??= new();
        PostingTransaction.Require(dto.Code.Length is > 0 and <= 50 && dto.Name.Length is > 0 and <= 200, "error.validation.required");
        PostingTransaction.Require(DataScopeModes.All.Contains(dto.DefaultScopeMode) && dto.DefaultScopeMode != DataScopeModes.CustomBranches
            && dto.RankOrder is >= 1 and <= 9, "error.validation.request");
        RequireOwner(dto.CompanyId);
        PostingTransaction.Require(dto.CompanyId == null || await Context.Branches.IgnoreQueryFilters()
            .AnyAsync(b => b.Id == dto.CompanyId && !b.IsDeleted && b.OrgUnitType == OrgUnitType.Company, cancellationToken), "error.validation.reference");
        PostingTransaction.Require(dto.DepartmentId == null || await Context.Departments.AnyAsync(d => d.Id == dto.DepartmentId && d.CompanyId == dto.CompanyId, cancellationToken), "error.validation.reference");
        PostingTransaction.Require(!await Context.JobPositions.AnyAsync(p => p.Id != id && p.CompanyId == dto.CompanyId && p.Code == dto.Code, cancellationToken), "error.validation.duplicate", 409);
    }
}

/// <summary>
/// Employee profiles. Creating one also writes the hire work-history row; later org moves go through
/// work history, which refreshes the cached org fields here.
/// </summary>
public class EmployeeService(ApplicationDbContext context, IIamService iam, ICurrentUser? user = null)
    : BaseCrudService<Employee, EmployeeDto, CreateEmployeeRequest, UpdateEmployeeRequest>(context)
{
    private readonly HrmScope _scope = new(user);

    protected override IQueryable<Employee> PrepareQuery(IQueryable<Employee> query)
    {
        if (_scope.All) return query;
        var branches = _scope.Branches; var departments = _scope.Departments; var own = _scope.OwnUserId;
        return query.Where(e => branches.Contains(e.BranchId) || (e.DepartmentId != null && departments.Contains(e.DepartmentId.Value)) || (own != null && e.UserId == own));
    }

    protected override async Task<Employee?> FindEntityAsync(int id, CancellationToken cancellationToken) =>
        await PrepareQuery(DbSet).FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    protected override IQueryable<Employee> ApplyFilters(IQueryable<Employee> query, BaseFilterQuery filter)
    {
        query = base.ApplyFilters(query, filter);
        return filter is EmployeeFilterQuery { WithoutAccount: { } withoutAccount }
            ? query.Where(e => (e.UserId == null) == withoutAccount)
            : query;
    }

    protected override async Task BeforeCreateAsync(Employee entity, CreateEmployeeRequest dto, CancellationToken cancellationToken)
    {
        await ValidateAsync(null, dto.Code, dto.FullName, dto.UserId, cancellationToken);
        await OrgRules.RequirePlacementAsync(Context, dto.CompanyId, dto.BranchId, dto.DepartmentId, dto.JobPositionId, cancellationToken);
        _scope.Require(dto.BranchId, dto.DepartmentId);
        entity.Code = dto.Code.Trim().ToUpperInvariant();
        entity.FullName = dto.FullName.Trim();
        entity.WorkHistories.Add(new WorkHistory
        {
            DateEffective = dto.HireDate ?? DateOnly.FromDateTime(DateTime.UtcNow),
            CompanyId = dto.CompanyId,
            BranchId = dto.BranchId,
            DepartmentId = dto.DepartmentId,
            JobPositionId = dto.JobPositionId,
            Notes = "Hire"
        });
    }

    protected override async Task BeforeUpdateAsync(Employee entity, UpdateEmployeeRequest dto, CancellationToken cancellationToken)
    {
        await ValidateAsync(entity.Id, dto.Code, dto.FullName, dto.UserId, cancellationToken);
        _scope.Require(entity.BranchId, entity.DepartmentId);
        dto.Code = dto.Code.Trim().ToUpperInvariant();
        dto.FullName = dto.FullName.Trim();
    }

    protected override Task BeforeDeleteAsync(Employee entity, CancellationToken cancellationToken)
    {
        _scope.Require(entity.BranchId, entity.DepartmentId);
        return Task.CompletedTask;
    }

    protected override Task AfterCreateAsync(Employee entity, CreateEmployeeRequest dto, CancellationToken cancellationToken) { iam.Invalidate(); return Task.CompletedTask; }
    protected override Task AfterUpdateAsync(Employee entity, UpdateEmployeeRequest dto, CancellationToken cancellationToken) { iam.Invalidate(); return Task.CompletedTask; }
    protected override Task AfterDeleteAsync(Employee entity, CancellationToken cancellationToken) { iam.Invalidate(); return Task.CompletedTask; }

    private async Task ValidateAsync(int? id, string code, string fullName, int? userId, CancellationToken cancellationToken)
    {
        var normalized = (code ?? "").Trim().ToUpperInvariant();
        PostingTransaction.Require(normalized.Length is > 0 and <= 50 && (fullName ?? "").Trim().Length is > 0 and <= 255, "error.validation.required");
        PostingTransaction.Require(!await Context.Employees.AnyAsync(e => e.Id != id && e.Code == normalized, cancellationToken), "error.validation.duplicate", 409);
        PostingTransaction.Require(userId == null || await Context.AppUsers.AnyAsync(u => u.Id == userId, cancellationToken), "error.validation.reference");
        PostingTransaction.Require(userId == null || !await Context.Employees.AnyAsync(e => e.Id != id && e.UserId == userId, cancellationToken), "error.hrm.user_linked", 409);
    }
}

/// <summary>
/// Work-history (QTCT) rows: the source of truth for an employee's company, branch, department and job
/// position. Every change refreshes the employee's cached org fields from the row in effect today.
/// </summary>
public class WorkHistoryService(ApplicationDbContext context, IIamService iam, ICurrentUser? user = null)
    : BaseCrudService<WorkHistory, WorkHistoryDto, CreateWorkHistoryRequest, UpdateWorkHistoryRequest>(context)
{
    private readonly HrmScope _scope = new(user);

    protected override IQueryable<WorkHistory> PrepareQuery(IQueryable<WorkHistory> query)
    {
        if (_scope.All) return query;
        var branches = _scope.Branches; var departments = _scope.Departments; var own = _scope.OwnUserId;
        return query.Where(w => branches.Contains(w.Employee!.BranchId) || (w.Employee.DepartmentId != null && departments.Contains(w.Employee.DepartmentId.Value))
            || (own != null && w.Employee.UserId == own));
    }

    protected override async Task<WorkHistory?> FindEntityAsync(int id, CancellationToken cancellationToken) =>
        await PrepareQuery(DbSet).FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

    protected override Task BeforeCreateAsync(WorkHistory entity, CreateWorkHistoryRequest dto, CancellationToken cancellationToken) =>
        ValidateAsync(dto, cancellationToken);

    protected override async Task BeforeUpdateAsync(WorkHistory entity, UpdateWorkHistoryRequest dto, CancellationToken cancellationToken)
    {
        PostingTransaction.Require(dto.EmployeeId == entity.EmployeeId, "error.validation.request");
        await ValidateAsync(dto, cancellationToken);
    }

    protected override Task AfterCreateAsync(WorkHistory entity, CreateWorkHistoryRequest dto, CancellationToken cancellationToken) => RefreshEmployeeAsync(entity.EmployeeId, cancellationToken);
    protected override Task AfterUpdateAsync(WorkHistory entity, UpdateWorkHistoryRequest dto, CancellationToken cancellationToken) => RefreshEmployeeAsync(entity.EmployeeId, cancellationToken);
    protected override Task AfterDeleteAsync(WorkHistory entity, CancellationToken cancellationToken) => RefreshEmployeeAsync(entity.EmployeeId, cancellationToken);

    private async Task ValidateAsync(CreateWorkHistoryRequest dto, CancellationToken cancellationToken)
    {
        PostingTransaction.Require(dto.DateEffective != default && (dto.Notes?.Length ?? 0) <= 500, "error.validation.required");
        var target = await Context.Employees.AsNoTracking().Where(e => e.Id == dto.EmployeeId).Select(e => new { e.BranchId, e.DepartmentId }).FirstOrDefaultAsync(cancellationToken);
        PostingTransaction.Require(target != null, "error.validation.reference");
        _scope.Require(target!.BranchId, target.DepartmentId);
        await OrgRules.RequirePlacementAsync(Context, dto.CompanyId, dto.BranchId, dto.DepartmentId, dto.JobPositionId, cancellationToken);
        _scope.Require(dto.BranchId, dto.DepartmentId);
    }

    /// <summary>Copies the approved row in effect today onto the employee; a termination deactivates the profile.</summary>
    private async Task RefreshEmployeeAsync(int employeeId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var current = await Context.WorkHistories.AsNoTracking()
            .Where(w => w.EmployeeId == employeeId && w.IsApproved && w.DateEffective <= today)
            .OrderByDescending(w => w.DateEffective).ThenByDescending(w => w.CreatedAtUtc).ThenByDescending(w => w.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var employee = await Context.Employees.FirstAsync(e => e.Id == employeeId, cancellationToken);
        if (current != null)
        {
            employee.IsActive = !current.IsTermination;
            if (!current.IsTermination)
            {
                employee.CompanyId = current.CompanyId;
                employee.BranchId = current.BranchId;
                employee.DepartmentId = current.DepartmentId;
                employee.JobPositionId = current.JobPositionId;
            }
            await Context.SaveChangesAsync(cancellationToken);
        }
        iam.Invalidate();
    }
}
