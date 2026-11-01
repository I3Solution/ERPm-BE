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
    /// <summary>Concurrency token; send it back on update to get 409 when someone else saved first.</summary>
    public uint Version { get; set; }
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
    /// <summary>Version read with the role; when present an update fails with 409 if the role changed meanwhile.</summary>
    public uint? Version { get; set; }
}

public class JobPositionRolesRequest
{
    public int[] RoleIds { get; set; } = [];
}

/// <summary>Role granted to a department; <see cref="ApplyToChildren"/> extends it to all sub-departments.</summary>
public class DepartmentRoleGrantDto
{
    public int RoleId { get; set; }
    public bool ApplyToChildren { get; set; }
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

/// <summary>Where a statement that matches an action came from, e.g. <c>job-position:SALES_ACCOUNTANT</c> or <c>inline-policy</c>.</summary>
public sealed record EffectiveSourceDto(string Source, string? Sid, string Effect);

public static class EffectiveStatus
{
    public const string Allowed = "Allowed";
    /// <summary>An explicit Deny statement matches, so the action is refused whatever else grants it.</summary>
    public const string Denied = "Denied";
    public const string NotGranted = "NotGranted";
}

/// <summary>One catalog action with its outcome for an account and every statement that matched it.</summary>
public sealed record EffectivePermissionDto(string Action, string Module, string Resource, string Verb, string Status, List<EffectiveSourceDto> Sources);

public sealed record DataScopeDto(bool Unrestricted, int[] BranchIds, int[] DepartmentIds, bool OwnRecords);

/// <summary>Signed-in account with its organization position and effective permissions (for the FE <c>can()</c>).</summary>
public sealed record CurrentAccountDto(
    int Id, string UserName, string DisplayName, string Role, int[] BranchIds,
    bool IsSuperAdmin, int? EmployeeId, int? CompanyId, int? PrimaryBranchId, int? DepartmentId, int? JobPositionId,
    string[] Permissions, DataScopeDto ReadScope);
