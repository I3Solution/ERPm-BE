using ERPm.Domain.Common;
using ERPm.Domain.Entities.HumanResource;

namespace ERPm.Domain.Entities.System;

/// <summary>
/// Dynamic IAM role (tbl_SYS_Role). Group template when <see cref="CompanyId"/> is null,
/// otherwise owned and managed by one company.
/// </summary>
public class IamRole : BaseAuditableEntity
{
    public const string AdminCode = "ADMIN";
    public const string OperatorCode = "OPERATOR";
    public const string ViewerCode = "VIEWER";

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Dictionary<string, string> Translations { get; set; } = new();
    public int? CompanyId { get; set; }
    public bool IsSystemRole { get; set; }                  // Code-defined role, cannot be edited or deleted
    public IamPolicyDocument PolicyDocument { get; set; } = new();
    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual Branch? Company { get; set; }
}

/// <summary>Default role of a job position (tbl_SYS_JobPositionRole), inherited by every holder.</summary>
public class JobPositionRole : BaseEntity
{
    public int JobPositionId { get; set; }
    public int RoleId { get; set; }

    // Navigation
    public virtual JobPosition? JobPosition { get; set; }
    public virtual IamRole? Role { get; set; }
}

/// <summary>Extra or concurrent role granted directly to an account (tbl_SYS_UserRole).</summary>
public class UserRole : BaseEntity
{
    public int UserId { get; set; }
    public int RoleId { get; set; }
    public IamDataScope? ScopeOverride { get; set; }        // Replaces the role statements' data scope for this user

    // Navigation
    public virtual AppUser? User { get; set; }
    public virtual IamRole? Role { get; set; }
}
