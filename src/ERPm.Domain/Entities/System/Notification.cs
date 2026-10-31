using ERPm.Domain.Common;

namespace ERPm.Domain.Entities.System;

/// <summary>
/// Root notification record generated from a business event.
/// Table: tbl_SYS_Notification
/// </summary>
public class Notification : BaseAuditableEntity
{
    public string TemplateCode { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = "INFO";
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public Dictionary<string, object> DataPayload { get; set; } = new();
    public string? ActionUrl { get; set; }
    public string TargetType { get; set; } = "USER";
    public int? TargetId { get; set; }
    public int? CompanyId { get; set; }
    public int? BranchId { get; set; }
    public int? SenderUserId { get; set; }
    public string? IdempotencyKey { get; set; }

    // Navigation
    public virtual Branch? Company { get; set; }
    public virtual Branch? Branch { get; set; }
    public virtual AppUser? SenderUser { get; set; }
    public virtual ICollection<NotificationRecipient> Recipients { get; set; } = new List<NotificationRecipient>();
    public virtual ICollection<NotificationOutbox> Outboxes { get; set; } = new List<NotificationOutbox>();
}
