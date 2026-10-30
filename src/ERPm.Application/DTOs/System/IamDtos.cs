using ERPm.Domain.Entities.System;

namespace ERPm.Application.DTOs.System;

public class RoleDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Dictionary<string, string> Translations { get; set; } = new();
    public int? CompanyId { get; set; }
    public bool IsSystemRole { get; set; }
    public IamPolicyDocument PolicyDocument { get; set; } = new();
    public bool IsActive { get; set; }
}

public class SaveRoleRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Dictionary<string, string> Translations { get; set; } = new();
    public int? CompanyId { get; set; }
    public IamPolicyDocument PolicyDocument { get; set; } = new();
    public bool IsActive { get; set; } = true;
}

public class JobPositionRolesRequest
{
    public int[] RoleIds { get; set; } = [];
}

public class UserRoleGrantDto
{
    public int RoleId { get; set; }
    public IamDataScope? ScopeOverride { get; set; }
}

/// <summary>Direct (exception) access of one account: extra roles plus an optional inline policy.</summary>
public class UserAccessPolicyDto
{
    public int UserId { get; set; }
    public List<UserRoleGrantDto> Roles { get; set; } = [];
    public IamPolicyDocument? InlinePolicy { get; set; }
}

public sealed record PermissionCatalogItemDto(string Action, string Module, string Resource, string Verb);

public sealed record DataScopeDto(bool Unrestricted, int[] BranchIds, int[] DepartmentIds, bool OwnRecords);

/// <summary>Signed-in account with its organization position and effective permissions (for the FE <c>can()</c>).</summary>
public sealed record CurrentAccountDto(
    int Id, string UserName, string DisplayName, string Role, int[] BranchIds,
    bool IsSuperAdmin, int? EmployeeId, int? CompanyId, int? PrimaryBranchId, int? DepartmentId, int? JobPositionId,
    string[] Permissions, DataScopeDto ReadScope);
