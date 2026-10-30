using ERPm.Domain.Common;
using ERPm.Domain.Entities.System;

namespace ERPm.Domain.Entities.HumanResource;

/// <summary>
/// Job position (tbl_HRM_JobPosition). Group-wide when <see cref="CompanyId"/> is null.
/// Holders inherit the position's roles and default data scope.
/// </summary>
public class JobPosition : BaseAuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Translations { get; set; } = new();
    public int? CompanyId { get; set; }
    public int? DepartmentId { get; set; }
    public int RankOrder { get; set; } = 5;                  // 1 = director ... 5 = staff
    public string DefaultScopeMode { get; set; } = DataScopeModes.Branch;
    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual Branch? Company { get; set; }
    public virtual Department? Department { get; set; }
    public virtual ICollection<JobPositionRole> Roles { get; set; } = new List<JobPositionRole>();
}
