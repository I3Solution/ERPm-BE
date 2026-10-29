using ERPm.Application.DTOs.HumanResource;
using ERPm.Domain.Entities.HumanResource;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;

namespace ERPm.Infrastructure.Services.HumanResource;

/// <summary>
/// Employee profiles. A linked account must exist and can belong to one employee only (unique index).
/// </summary>
public class EmployeeService(ApplicationDbContext context)
    : BaseCrudService<Employee, EmployeeDto, CreateEmployeeRequest, UpdateEmployeeRequest>(context)
{
    protected override async Task BeforeCreateAsync(Employee entity, CreateEmployeeRequest dto, CancellationToken cancellationToken)
    {
        await ValidateAsync(dto, cancellationToken);
        entity.Code = dto.Code;
        entity.Name = dto.Name;
    }

    protected override Task BeforeUpdateAsync(Employee entity, UpdateEmployeeRequest dto, CancellationToken cancellationToken) =>
        ValidateAsync(dto, cancellationToken);

    private async Task ValidateAsync(CreateEmployeeRequest dto, CancellationToken cancellationToken)
    {
        dto.Code = dto.Code.Trim();
        dto.Name = dto.Name.Trim();
        PostingTransaction.Require(dto.Code.Length > 0 && dto.Name.Length > 0, "error.validation.required");
        PostingTransaction.Require(dto.UserId == null || await Context.AppUsers.AnyAsync(u => u.Id == dto.UserId, cancellationToken), "error.validation.reference");
    }
}

/// <summary>
/// Work-history (QTCT) rows. Non-termination rows need an org unit; a missing legal entity is taken from the org unit.
/// </summary>
public class WorkHistoryService(ApplicationDbContext context)
    : BaseCrudService<WorkHistory, WorkHistoryDto, CreateWorkHistoryRequest, UpdateWorkHistoryRequest>(context)
{
    protected override async Task BeforeCreateAsync(WorkHistory entity, CreateWorkHistoryRequest dto, CancellationToken cancellationToken)
    {
        await ValidateAsync(dto, cancellationToken);
        entity.CompanyId = dto.CompanyId;
    }

    protected override Task BeforeUpdateAsync(WorkHistory entity, UpdateWorkHistoryRequest dto, CancellationToken cancellationToken) =>
        ValidateAsync(dto, cancellationToken);

    private async Task ValidateAsync(CreateWorkHistoryRequest dto, CancellationToken cancellationToken)
    {
        PostingTransaction.Require(dto.DateEffective != default, "error.validation.required");
        PostingTransaction.Require(dto.IsTermination || dto.OrgStructureId != null, "error.validation.required");
        PostingTransaction.Require(await Context.Employees.AnyAsync(e => e.Id == dto.EmployeeId, cancellationToken), "error.validation.reference");
        PostingTransaction.Require(dto.CompanyId == null || await Context.Companies.AnyAsync(c => c.Id == dto.CompanyId, cancellationToken), "error.validation.reference");
        if (dto.OrgStructureId is not { } orgId) return;

        var org = await Context.OrgStructures.AsNoTracking().Where(o => o.Id == orgId).Select(o => new { o.CompanyId }).FirstOrDefaultAsync(cancellationToken);
        PostingTransaction.Require(org != null, "error.validation.reference");
        PostingTransaction.Require(dto.CompanyId == null || org!.CompanyId == null || org.CompanyId == dto.CompanyId, "error.validation.reference");
        dto.CompanyId ??= org!.CompanyId;
    }
}

/// <summary>
/// Shift catalogue. Rejects empty or negative timings so <c>TimesheetCalculator</c> always gets a usable schedule.
/// </summary>
public class ShiftService(ApplicationDbContext context)
    : BaseCrudService<Shift, ShiftDto, CreateShiftRequest, UpdateShiftRequest>(context)
{
    protected override Task BeforeCreateAsync(Shift entity, CreateShiftRequest dto, CancellationToken cancellationToken)
    {
        Validate(dto);
        entity.Code = dto.Code;
        entity.Name = dto.Name;
        return Task.CompletedTask;
    }

    protected override Task BeforeUpdateAsync(Shift entity, UpdateShiftRequest dto, CancellationToken cancellationToken)
    {
        Validate(dto);
        return Task.CompletedTask;
    }

    private static void Validate(CreateShiftRequest dto)
    {
        dto.Code = dto.Code.Trim().ToUpperInvariant();
        dto.Name = dto.Name.Trim();
        PostingTransaction.Require(dto.Code.Length > 0 && dto.Name.Length > 0, "error.validation.required");
        PostingTransaction.Require(dto.StartTime != dto.EndTime && dto.BreakMinutes >= 0 && dto.LateGraceMinutes >= 0, "error.validation.request");
        var schedule = new Domain.Services.HumanResource.ShiftSchedule(dto.StartTime, dto.EndTime, dto.BreakMinutes, dto.LateGraceMinutes, dto.WorkDays);
        PostingTransaction.Require(schedule.StandardMinutes > 0, "error.validation.request");
    }
}

/// <summary>
/// HRM data-permission rows. Each row needs an existing account and at least one dimension.
/// </summary>
public class HrmDataPermissionService(ApplicationDbContext context)
    : BaseCrudService<HrmDataPermission, HrmDataPermissionDto, CreateHrmDataPermissionRequest, UpdateHrmDataPermissionRequest>(context)
{
    protected override Task BeforeCreateAsync(HrmDataPermission entity, CreateHrmDataPermissionRequest dto, CancellationToken cancellationToken) =>
        ValidateAsync(dto, cancellationToken);

    protected override Task BeforeUpdateAsync(HrmDataPermission entity, UpdateHrmDataPermissionRequest dto, CancellationToken cancellationToken) =>
        ValidateAsync(dto, cancellationToken);

    private async Task ValidateAsync(CreateHrmDataPermissionRequest dto, CancellationToken cancellationToken)
    {
        PostingTransaction.Require(dto.OrgStructureId != null || dto.CompanyId != null, "error.validation.required");
        PostingTransaction.Require(await Context.AppUsers.AnyAsync(u => u.Id == dto.UserId, cancellationToken), "error.validation.reference");
        PostingTransaction.Require(dto.OrgStructureId == null || await Context.OrgStructures.AnyAsync(o => o.Id == dto.OrgStructureId, cancellationToken), "error.validation.reference");
        PostingTransaction.Require(dto.CompanyId == null || await Context.Companies.AnyAsync(c => c.Id == dto.CompanyId, cancellationToken), "error.validation.reference");
    }
}
