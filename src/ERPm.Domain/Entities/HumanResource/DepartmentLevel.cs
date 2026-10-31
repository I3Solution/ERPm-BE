using ERPm.Domain.Common;
using ERPm.Domain.Entities.System;

namespace ERPm.Domain.Entities.HumanResource;

/// <summary>
/// Department level catalog (tbl_HRM_DepartmentLevel): Division, Department, Section, Team... A child
/// department must sit on a deeper level (larger <see cref="SortOrder"/>) than its parent.
/// Group-wide when <see cref="CompanyId"/> is null.
/// </summary>
public class DepartmentLevel : BaseAuditableEntity
{
    public const string DivisionCode = "DIVISION";
    public const string DepartmentCode = "DEPARTMENT";
    public const string SectionCode = "SECTION";
    public const string TeamCode = "TEAM";

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Translations { get; set; } = new();
    public int SortOrder { get; set; }
    public int? CompanyId { get; set; }
    public string? StandardLevelCode { get; set; }           // Hook: maps a company level to a group standard level (M1)
    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual Branch? Company { get; set; }
}
