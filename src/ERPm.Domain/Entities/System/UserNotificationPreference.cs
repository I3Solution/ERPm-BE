using ERPm.Domain.Common;

namespace ERPm.Domain.Entities.System;

/// <summary>
/// User preference configuring delivery channels and quiet hours per module category.
/// Table: tbl_SYS_UserNotificationPreference
/// </summary>
public class UserNotificationPreference : BaseAuditableEntity
{
    public int UserId { get; set; }
    public string Category { get; set; } = "ALL";
    public string Channel { get; set; } = "IN_APP";
    public bool IsEnabled { get; set; } = true;
    public TimeOnly? QuietHoursStart { get; set; }
    public TimeOnly? QuietHoursEnd { get; set; }

    // Navigation
    public virtual AppUser? User { get; set; }
}
