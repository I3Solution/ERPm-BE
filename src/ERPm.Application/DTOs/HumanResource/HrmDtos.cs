using ERPm.Domain.Entities.HumanResource;
using ERPm.Domain.Services.HumanResource;

namespace ERPm.Application.DTOs.HumanResource;

public class EmployeeDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Gender Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? IdentityNumber { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public DateOnly? HireDate { get; set; }
    public int? UserId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CreateEmployeeRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Gender Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? IdentityNumber { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public DateOnly? HireDate { get; set; }
    public int? UserId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateEmployeeRequest : CreateEmployeeRequest
{
}

public class WorkHistoryDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public DateOnly DateEffective { get; set; }
    public int? OrgStructureId { get; set; }
    public int? CompanyId { get; set; }
    public string? JobTitle { get; set; }
    public bool IsTermination { get; set; }
    public bool IsApproved { get; set; } = true;
    public string? Notes { get; set; }
}

public class CreateWorkHistoryRequest
{
    public int EmployeeId { get; set; }
    public DateOnly DateEffective { get; set; }
    public int? OrgStructureId { get; set; }
    public int? CompanyId { get; set; }
    public string? JobTitle { get; set; }
    public bool IsTermination { get; set; }
    public bool IsApproved { get; set; } = true;
    public string? Notes { get; set; }
}

public class UpdateWorkHistoryRequest : CreateWorkHistoryRequest
{
}

/// <summary>Approved work history of one employee and its split by legal entity for a period.</summary>
public class EmployeeTimelineDto
{
    public int EmployeeId { get; set; }
    public List<TimelineEntry> Entries { get; set; } = [];
    public List<EmploymentSpan> EmploymentSegments { get; set; } = [];
}

public class ShiftDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int BreakMinutes { get; set; }
    public int LateGraceMinutes { get; set; }
    public WorkDays WorkDays { get; set; }
    public bool IsActive { get; set; } = true;
}

public class CreateShiftRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int BreakMinutes { get; set; }
    public int LateGraceMinutes { get; set; }
    public WorkDays WorkDays { get; set; } = WorkDays.MonToFri;
    public bool IsActive { get; set; } = true;
}

public class UpdateShiftRequest : CreateShiftRequest
{
}

public class HrmDataPermissionDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? OrgStructureId { get; set; }
    public int? CompanyId { get; set; }
}

public class CreateHrmDataPermissionRequest
{
    public int UserId { get; set; }
    public int? OrgStructureId { get; set; }
    public int? CompanyId { get; set; }
}

public class UpdateHrmDataPermissionRequest : CreateHrmDataPermissionRequest
{
}
