using ERPm.Domain.Common;

namespace ERPm.Domain.Entities.System;

/// <summary>
/// Asynchronous outbox queue for external notification delivery channels (Email, FCM, SMS).
/// Table: tbl_SYS_NotificationOutbox
/// </summary>
public class NotificationOutbox : BaseAuditableEntity
{
    public int NotificationId { get; set; }
    public int UserId { get; set; }
    public string Channel { get; set; } = "EMAIL";
    public string Destination { get; set; } = string.Empty;
    public Dictionary<string, object> Payload { get; set; } = new();
    public string Status { get; set; } = "PENDING";
    public int RetryCount { get; set; } = 0;
    public DateTime NextRetryUtc { get; set; } = DateTime.UtcNow;
    public string? LastError { get; set; }

    // Navigation
    public virtual Notification? Notification { get; set; }
    public virtual AppUser? User { get; set; }
}
