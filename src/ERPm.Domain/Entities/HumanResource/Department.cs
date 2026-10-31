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
    public int LevelId { get; set; }
    public string Path { get; set; } = "/";                  // Ancestor ids incl. itself, e.g. "/1/5/12/"; filter subtrees with LIKE
    public int Depth { get; set; }                           // Number of ids in Path (root = 1)
    public DateOnly? EffectiveFrom { get; set; }             // Hook for time-based restructuring (M2), no logic yet
    public DateOnly? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual Branch? Company { get; set; }
    public virtual Branch? Branch { get; set; }
    public virtual Department? Parent { get; set; }
    public virtual DepartmentLevel? Level { get; set; }
    public virtual ICollection<Department> Children { get; set; } = new List<Department>();
}
