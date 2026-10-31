namespace ERPm.Domain.Events;

/// <summary>
/// Domain contract for business notification events published across all ERP modules.
/// </summary>
public interface INotificationEvent
{
    string TemplateCode { get; }
    string Category { get; }
    string Severity { get; }
    string TargetType { get; }
    int? TargetId { get; }
    int? CompanyId { get; }
    int? BranchId { get; }
    int? SenderUserId { get; }
    string? IdempotencyKey { get; }
    string? ActionUrl { get; }
    IDictionary<string, object> Parameters { get; }
}

/// <summary>
/// Default immutable class implementing <see cref="INotificationEvent"/>.
/// </summary>
public class NotificationEvent : INotificationEvent
{
    public NotificationEvent(
        string templateCode,
        string category,
        string severity,
        string targetType,
        int? targetId = null,
        int? companyId = null,
        int? branchId = null,
        int? senderUserId = null,
        string? idempotencyKey = null,
        string? actionUrl = null,
        IDictionary<string, object>? parameters = null)
    {
        TemplateCode = templateCode;
        Category = category;
        Severity = severity;
        TargetType = targetType;
        TargetId = targetId;
        CompanyId = companyId;
        BranchId = branchId;
        SenderUserId = senderUserId;
        IdempotencyKey = idempotencyKey;
        ActionUrl = actionUrl;
        Parameters = parameters ?? new Dictionary<string, object>();
    }

    public string TemplateCode { get; init; }
    public string Category { get; init; }
    public string Severity { get; init; }
    public string TargetType { get; init; }
    public int? TargetId { get; init; }
    public int? CompanyId { get; init; }
    public int? BranchId { get; init; }
    public int? SenderUserId { get; init; }
    public string? IdempotencyKey { get; init; }
    public string? ActionUrl { get; init; }
    public IDictionary<string, object> Parameters { get; init; }
}
