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

/// <summary>Departments of a company branch. Rejects cycles and parents from another company.</summary>
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

    protected override async Task<Department?> FindEntityAsync(int id, CancellationToken cancellationToken) =>
        await PrepareQuery(DbSet).FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    protected override async Task BeforeCreateAsync(Department entity, CreateDepartmentRequest dto, CancellationToken cancellationToken)
    {
        await ValidateAsync(null, dto, cancellationToken);
        entity.Code = dto.Code;
        entity.Name = dto.Name;
    }

    protected override Task BeforeUpdateAsync(Department entity, UpdateDepartmentRequest dto, CancellationToken cancellationToken) =>
        ValidateAsync(entity.Id, dto, cancellationToken);

    protected override async Task BeforeDeleteAsync(Department entity, CancellationToken cancellationToken)
    {
        var inUse = await Context.Departments.AnyAsync(d => d.ParentId == entity.Id, cancellationToken)
            || await Context.Employees.AnyAsync(e => e.DepartmentId == entity.Id, cancellationToken)
            || await Context.JobPositions.AnyAsync(p => p.DepartmentId == entity.Id, cancellationToken);
        PostingTransaction.Require(!inUse, "error.item.in_use", 409);
    }

    protected override Task AfterUpdateAsync(Department entity, UpdateDepartmentRequest dto, CancellationToken cancellationToken) { iam.Invalidate(); return Task.CompletedTask; }
    protected override Task AfterDeleteAsync(Department entity, CancellationToken cancellationToken) { iam.Invalidate(); return Task.CompletedTask; }
    protected override Task AfterCreateAsync(Department entity, CreateDepartmentRequest dto, CancellationToken cancellationToken) { iam.Invalidate(); return Task.CompletedTask; }

    private async Task ValidateAsync(int? id, CreateDepartmentRequest dto, CancellationToken cancellationToken)
    {
        dto.Code = (dto.Code ?? "").Trim().ToUpperInvariant();
        dto.Name = (dto.Name ?? "").Trim();
        dto.Translations ??= new();
        PostingTransaction.Require(dto.Code.Length is > 0 and <= 50 && dto.Name.Length is > 0 and <= 200, "error.validation.required");
        await OrgRules.RequirePlacementAsync(Context, dto.CompanyId, dto.BranchId, null, null, cancellationToken);
        _scope.Require(dto.BranchId, id);
        PostingTransaction.Require(!await Context.Departments.AnyAsync(d => d.Id != id && d.CompanyId == dto.CompanyId && d.Code == dto.Code, cancellationToken), "error.validation.duplicate", 409);
        if (dto.ParentId is not { } parentId) return;

        var parents = await Context.Departments.AsNoTracking().Select(d => new { d.Id, d.ParentId, d.CompanyId }).ToDictionaryAsync(d => d.Id, cancellationToken);
        PostingTransaction.Require(parents.TryGetValue(parentId, out var parent) && parent.CompanyId == dto.CompanyId, "error.validation.reference");
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
/// Job positions. Group-wide positions (no company) need an unrestricted scope; company positions need the
/// company inside the caller's scope.
/// </summary>
public class JobPositionService(ApplicationDbContext context, IIamService iam, ICurrentUser? user = null)
    : BaseCrudService<JobPosition, JobPositionDto, CreateJobPositionRequest, UpdateJobPositionRequest>(context)
{
    private readonly HrmScope _scope = new(user);

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
