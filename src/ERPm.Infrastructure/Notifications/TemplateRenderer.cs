using System.Text.RegularExpressions;
using ERPm.Application.Common.Notifications;
using ERPm.Domain.Entities.System;

namespace ERPm.Infrastructure.Notifications;

/// <summary>
/// Renders templates by substituting {placeholder} tokens from parameters dictionary
/// with support for localized multilingual strings.
/// </summary>
public partial class TemplateRenderer : ITemplateRenderer
{
    [GeneratedRegex(@"\{([a-zA-Z0-9_]+)\}")]
    private static partial Regex PlaceholderRegex();

    public (string Title, string Body) Render(NotificationTemplate template, IDictionary<string, object> parameters, string languageCode = "vi")
    {
        string titlePattern = template.TitleTemplate;
        string bodyPattern = template.BodyTemplate;

        if (!string.IsNullOrEmpty(languageCode) &&
            template.Translations.TryGetValue(languageCode, out var translation) &&
            translation != null)
        {
            if (!string.IsNullOrWhiteSpace(translation.Title))
                titlePattern = translation.Title;
            if (!string.IsNullOrWhiteSpace(translation.Body))
                bodyPattern = translation.Body;
        }

        string title = Format(titlePattern, parameters);
        string body = Format(bodyPattern, parameters);

        return (title, body);
    }

    private static string Format(string pattern, IDictionary<string, object> parameters)
    {
        if (string.IsNullOrEmpty(pattern) || parameters.Count == 0) return pattern;

        return PlaceholderRegex().Replace(pattern, match =>
        {
            var key = match.Groups[1].Value;
            if (parameters.TryGetValue(key, out var val) && val != null)
            {
                return val switch
                {
                    decimal d => d.ToString("N0"),
                    double db => db.ToString("N0"),
                    int i => i.ToString(),
                    DateTime dt => dt.ToString("dd/MM/yyyy HH:mm"),
                    DateOnly dOnly => dOnly.ToString("dd/MM/yyyy"),
                    _ => val.ToString() ?? string.Empty
                };
            }
            return match.Value;
        });
    }
}
