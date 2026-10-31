using ERPm.Domain.Common;

namespace ERPm.Domain.Entities.System;

/// <summary>
/// Translation container for title and body template strings.
/// </summary>
public class NotificationTemplateTranslation
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

/// <summary>
/// Pre-configured template defining message format, channels, and severity for system events.
/// Table: tbl_SYS_NotificationTemplate
/// </summary>
public class NotificationTemplate : BaseAuditableEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string DefaultSeverity { get; set; } = "INFO";
    public List<string> SupportedChannels { get; set; } = ["IN_APP"];
    public string TitleTemplate { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
    public Dictionary<string, NotificationTemplateTranslation> Translations { get; set; } = new();
    public string? ActionUrlPattern { get; set; }
    public bool IsDigestAllowed { get; set; } = false;
    public int DigestWindowSeconds { get; set; } = 0;
    public bool IsActive { get; set; } = true;
}
