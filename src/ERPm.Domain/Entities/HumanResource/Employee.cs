using ERPm.Domain.Common;
using ERPm.Domain.Entities.System;

namespace ERPm.Domain.Entities.HumanResource;

/// <summary>
/// Employee profile (tbl_HRM_Employee). Org fields cache the current approved <see cref="WorkHistory"/> row,
/// which stays the source of truth.
/// </summary>
public class Employee : BaseAuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateOnly? HireDate { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public int? DepartmentId { get; set; }
    public int JobPositionId { get; set; }
    public int? UserId { get; set; }                         // Linked login account (one employee per account)
    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual Branch? Company { get; set; }
    public virtual Branch? Branch { get; set; }
    public virtual Department? Department { get; set; }
    public virtual JobPosition? JobPosition { get; set; }
    public virtual AppUser? User { get; set; }
    public virtual ICollection<WorkHistory> WorkHistories { get; set; } = new List<WorkHistory>();
}
