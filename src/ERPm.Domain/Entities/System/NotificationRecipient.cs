using ERPm.Domain.Common;

namespace ERPm.Domain.Entities.System;

/// <summary>
/// Per-user receipt and read status for a notification.
/// Table: tbl_SYS_NotificationRecipient
/// </summary>
public class NotificationRecipient : BaseAuditableEntity
{
    public int NotificationId { get; set; }
    public int UserId { get; set; }
    public bool IsRead { get; set; } = false;
    public DateTime? ReadAtUtc { get; set; }
    public bool IsArchived { get; set; } = false;

    // Navigation
    public virtual Notification? Notification { get; set; }
    public virtual AppUser? User { get; set; }
}
