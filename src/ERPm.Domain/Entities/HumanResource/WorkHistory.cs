using ERPm.Domain.Common;
using ERPm.Domain.Entities.System;

namespace ERPm.Domain.Entities.HumanResource;

/// <summary>
/// Work-history (QTCT) row (tbl_HRM_WorkHistory): hire, transfer or termination effective from
/// <see cref="DateEffective"/>. Only approved rows count.
/// </summary>
public class WorkHistory : BaseAuditableEntity
{
    public int EmployeeId { get; set; }
    public DateOnly DateEffective { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public int? DepartmentId { get; set; }
    public int JobPositionId { get; set; }
    public bool IsTermination { get; set; }
    public bool IsApproved { get; set; } = true;
    public string? Notes { get; set; }

    // Navigation
    public virtual Employee? Employee { get; set; }
    public virtual Branch? Company { get; set; }
    public virtual Branch? Branch { get; set; }
    public virtual Department? Department { get; set; }
    public virtual JobPosition? JobPosition { get; set; }
}
