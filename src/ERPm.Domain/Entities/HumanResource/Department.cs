using ERPm.Domain.Common;
using ERPm.Domain.Entities.System;

namespace ERPm.Domain.Entities.HumanResource;

/// <summary>Department or sub-unit of a company branch (tbl_HRM_Department).</summary>
public class Department : BaseAuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Translations { get; set; } = new();
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public int? ParentId { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual Branch? Company { get; set; }
    public virtual Branch? Branch { get; set; }
    public virtual Department? Parent { get; set; }
    public virtual ICollection<Department> Children { get; set; } = new List<Department>();
}
