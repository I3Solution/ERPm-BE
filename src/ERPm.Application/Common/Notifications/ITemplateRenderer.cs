using ERPm.Domain.Entities.System;

namespace ERPm.Application.Common.Notifications;

/// <summary>
/// Renders title and body templates with dynamic placeholders and multilingual translations.
/// </summary>
public interface ITemplateRenderer
{
    (string Title, string Body) Render(NotificationTemplate template, IDictionary<string, object> parameters, string languageCode = "vi");
    string RenderString(string pattern, IDictionary<string, object> parameters);
}
