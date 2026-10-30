using ERPm.Domain.Common;

namespace ERPm.Domain.Entities.System;

public class AppUser : BaseAuditableEntity
{
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Viewer";            // Legacy system role code, mapped to the matching system IAM role
    public bool IsSuperAdmin { get; set; }                  // Group-level administrator, bypasses IAM evaluation
    public IamPolicyDocument? InlinePolicy { get; set; }    // Per-user Allow/Deny override (jsonb)
    public List<int> BranchIds { get; set; } = [];
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public bool IsActive { get; set; } = true;
    public int FailedLoginCount { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
}
