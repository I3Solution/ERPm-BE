using ERPm.Domain.Common;
using ERPm.Domain.Entities.System;

namespace ERPm.Domain.Entities.HumanResource;

/// <summary>
/// HRM data-permission row of an account (HRM9: Sys_DataPermission). A row grants an org unit subtree,
/// a legal entity, or an org unit within a legal entity; see <see cref="Services.HumanResource.DataScope"/>.
/// Separate from the branch scope used by sales and inventory.
/// </summary>
public class HrmDataPermission : BaseAuditableEntity
{
    public int UserId { get; set; }
    public int? OrgStructureId { get; set; }
    public int? CompanyId { get; set; }

    // Navigation
    public virtual AppUser? User { get; set; }
    public virtual OrgStructure? OrgStructure { get; set; }
    public virtual Company? Company { get; set; }
}
