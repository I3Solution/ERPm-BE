using ERPm.Domain.Common;
using ERPm.Domain.Entities.System;
using ERPm.Domain.Services.HumanResource;

namespace ERPm.Domain.Entities.HumanResource;

/// <summary>
/// One work-history (QTCT) row: hire, transfer or termination effective from <see cref="DateEffective"/>.
/// Only approved rows drive visibility, edit rights and attendance/payroll segmentation.
/// </summary>
public class WorkHistory : BaseAuditableEntity
{
    public int EmployeeId { get; set; }
    public DateOnly DateEffective { get; set; }
    public int? OrgStructureId { get; set; }
    public int? CompanyId { get; set; }                       // Null inherits the previous row's legal entity
    public string? JobTitle { get; set; }
    public bool IsTermination { get; set; }
    public bool IsApproved { get; set; } = true;
    public string? Notes { get; set; }

    // Navigation
    public virtual Employee? Employee { get; set; }
    public virtual OrgStructure? OrgStructure { get; set; }
    public virtual Company? Company { get; set; }

    public TimelineEntry ToTimelineEntry() =>
        new(Id, DateEffective, OrgStructureId, CompanyId, IsTermination, CreatedAtUtc);
}
