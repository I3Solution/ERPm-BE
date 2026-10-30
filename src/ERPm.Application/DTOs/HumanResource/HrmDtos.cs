namespace ERPm.Application.DTOs.HumanResource;

public class DepartmentDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Translations { get; set; } = new();
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public int? ParentId { get; set; }
    public bool IsActive { get; set; }
}

public class CreateDepartmentRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Translations { get; set; } = new();
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public int? ParentId { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateDepartmentRequest : CreateDepartmentRequest
{
}

public class JobPositionDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Translations { get; set; } = new();
    public int? CompanyId { get; set; }
    public int? DepartmentId { get; set; }
    public int RankOrder { get; set; }
    public string DefaultScopeMode { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class CreateJobPositionRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Translations { get; set; } = new();
    public int? CompanyId { get; set; }
    public int? DepartmentId { get; set; }
    public int RankOrder { get; set; } = 5;
    public string DefaultScopeMode { get; set; } = "BRANCH";
    public bool IsActive { get; set; } = true;
}

public class UpdateJobPositionRequest : CreateJobPositionRequest
{
}

public class EmployeeDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateOnly? HireDate { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public int? DepartmentId { get; set; }
    public int JobPositionId { get; set; }
    public int? UserId { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>Org fields create the first (hire) work-history row; later moves go through work history.</summary>
public class CreateEmployeeRequest
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
    public int? UserId { get; set; }
}

public class UpdateEmployeeRequest
{
    public string Code { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateOnly? HireDate { get; set; }
    public int? UserId { get; set; }
}

public class WorkHistoryDto
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public DateOnly DateEffective { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public int? DepartmentId { get; set; }
    public int JobPositionId { get; set; }
    public bool IsTermination { get; set; }
    public bool IsApproved { get; set; }
    public string? Notes { get; set; }
}

public class CreateWorkHistoryRequest
{
    public int EmployeeId { get; set; }
    public DateOnly DateEffective { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }
    public int? DepartmentId { get; set; }
    public int JobPositionId { get; set; }
    public bool IsTermination { get; set; }
    public bool IsApproved { get; set; } = true;
    public string? Notes { get; set; }
}

public class UpdateWorkHistoryRequest : CreateWorkHistoryRequest
{
}
