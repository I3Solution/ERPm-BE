using ERPm.Domain.Common;

namespace ERPm.Domain.Entities.System;

/// <summary>
/// Organization unit tree (division / department / team). Shared master data: HRM work history and data
/// permissions reference it, and other modules may use it as requesting department or cost center.
/// Independent from <see cref="Branch"/>, which models selling/stock locations.
/// </summary>
public class OrgStructure : BaseAuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? ParentId { get; set; }
    public int? CompanyId { get; set; }                 // Legal entity the unit belongs to
    public int? BranchId { get; set; }                  // Physical branch the unit works at, if any
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual OrgStructure? Parent { get; set; }
    public virtual ICollection<OrgStructure> Children { get; set; } = new List<OrgStructure>();
    public virtual Company? Company { get; set; }
    public virtual Branch? Branch { get; set; }
}
