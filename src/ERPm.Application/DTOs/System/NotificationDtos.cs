namespace ERPm.Application.DTOs.System;

/// <summary>
/// Data transfer object for a notification delivered to a recipient.
/// </summary>
public class NotificationDto
{
    public int Id { get; set; }
    public Guid ExternalId { get; set; }
    public int NotificationId { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = "INFO";
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? ActionUrl { get; set; }
    public Dictionary<string, object> DataPayload { get; set; } = new();
    public DateTime CreatedAtUtc { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAtUtc { get; set; }
    public bool IsArchived { get; set; }
}

/// <summary>
/// Paginated list of notifications with unread counter.
/// </summary>
public class NotificationListResponse
{
    public List<NotificationDto> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int UnreadCount { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// Lightweight counter for top header bell badge.
/// </summary>
public record UnreadCountDto(int UnreadCount);

/// <summary>
/// Notification preference item for user settings matrix.
/// </summary>
public class UserNotificationPreferenceDto
{
    public int Id { get; set; }
    public string Category { get; set; } = "ALL";
    public string Channel { get; set; } = "IN_APP";
    public bool IsEnabled { get; set; } = true;
    public TimeOnly? QuietHoursStart { get; set; }
    public TimeOnly? QuietHoursEnd { get; set; }
}

/// <summary>
/// Request to update an individual notification channel preference.
/// </summary>
public class UpdatePreferenceRequest
{
    public string Category { get; set; } = "ALL";
    public string Channel { get; set; } = "IN_APP";
    public bool IsEnabled { get; set; } = true;
    public TimeOnly? QuietHoursStart { get; set; }
    public TimeOnly? QuietHoursEnd { get; set; }
}

/// <summary>
/// Request to broadcast an ad-hoc announcement or alert.
/// </summary>
public class BroadcastNotificationRequest
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Category { get; set; } = "SYS";
    public string Severity { get; set; } = "INFO";
    public string? ActionUrl { get; set; }
    public string TargetType { get; set; } = "ALL";
    public int? TargetId { get; set; }
    public int? BranchId { get; set; }
    public int? CompanyId { get; set; }
}

/// <summary>
/// Result returned by the notification publishing pipeline.
/// </summary>
public record NotificationResult(
    bool Success,
    int? NotificationId,
    int RecipientCount,
    string? ErrorMessage = null);
