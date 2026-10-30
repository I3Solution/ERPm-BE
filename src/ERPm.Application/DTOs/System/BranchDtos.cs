using ERPm.Domain.Entities.System;

namespace ERPm.Application.DTOs.System;

public class BranchDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? ParentId { get; set; }
    public string? ParentName { get; set; }
    public OrgUnitType OrgUnitType { get; set; }
    public int? CompanyId { get; set; }
    public string? TaxCode { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public List<BranchDto> Children { get; set; } = new();
}

public class CreateBranchRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? ParentId { get; set; }
    public OrgUnitType OrgUnitType { get; set; } = OrgUnitType.Branch;
    public string? TaxCode { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
}
