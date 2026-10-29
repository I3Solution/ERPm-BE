using ERPm.Domain.Common;

namespace ERPm.Domain.Entities.System;

/// <summary>
/// Legal entity (registered company) inside a tenant. Owns branches, org units and employees;
/// tax, social-insurance and payroll reporting are done per legal entity.
/// </summary>
public class Company : BaseAuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? TaxCode { get; set; }
    public string? SocialInsuranceCode { get; set; }    // Employer code at the social insurance agency
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual ICollection<Branch> Branches { get; set; } = new List<Branch>();
    public virtual ICollection<OrgStructure> OrgStructures { get; set; } = new List<OrgStructure>();
}
