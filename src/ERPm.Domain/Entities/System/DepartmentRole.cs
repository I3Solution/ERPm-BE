using ERPm.Domain.Common;
using ERPm.Domain.Entities.HumanResource;

namespace ERPm.Domain.Entities.System;

/// <summary>
/// Role granted to every employee of a department (tbl_SYS_DepartmentRole). With
/// <see cref="ApplyToChildren"/> it also reaches the employees of all sub-departments.
/// </summary>
public class DepartmentRole : BaseEntity
{
    public int DepartmentId { get; set; }
    public int RoleId { get; set; }
    public bool ApplyToChildren { get; set; }

    // Navigation
    public virtual Department? Department { get; set; }
    public virtual IamRole? Role { get; set; }
}
