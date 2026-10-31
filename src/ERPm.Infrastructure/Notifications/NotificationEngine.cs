using ERPm.Application.Common.Notifications;
using ERPm.Application.DTOs.System;
using ERPm.Domain.Entities.System;
using ERPm.Domain.Enums;
using ERPm.Domain.Events;
using ERPm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ERPm.Infrastructure.Notifications;

/// <summary>
/// Core notification processing pipeline: verifies idempotency, evaluates user preferences,
/// renders localized messages, persists recipients, and dispatches via channels.
/// Uses isolated IServiceScopeFactory for full transaction and concurrency decoupling.
/// </summary>
public class NotificationEngine : INotificationEngine
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITemplateRenderer _templateRenderer;
    private readonly ILogger<NotificationEngine> _logger;

    public NotificationEngine(
        IServiceScopeFactory scopeFactory,
        ITemplateRenderer templateRenderer,
        ILogger<NotificationEngine> logger)
    {
        _scopeFactory = scopeFactory;
        _templateRenderer = templateRenderer;
        _logger = logger;
    }

    public async Task<NotificationResult> PublishAsync(INotificationEvent notifEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var router = scope.ServiceProvider.GetRequiredService<INotificationRouter>();
            var channelProviders = scope.ServiceProvider.GetServices<INotificationChannelProvider>();

            // 1. Idempotency Guard
            if (!string.IsNullOrWhiteSpace(notifEvent.IdempotencyKey))
            {
                var existing = await context.Notifications
                    .AsNoTracking()
                    .FirstOrDefaultAsync(n => n.IdempotencyKey == notifEvent.IdempotencyKey, cancellationToken);

                if (existing != null)
                {
                    _logger.LogInformation("Skipping duplicate notification with idempotency key {Key}", notifEvent.IdempotencyKey);
                    return new NotificationResult(true, existing.Id, 0);
                }
            }

            // 2. Load Notification Template
            var template = await context.NotificationTemplates
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Code == notifEvent.TemplateCode && t.IsActive, cancellationToken);

            string title;
            string body;
            string? actionUrl = notifEvent.ActionUrl;
            string severity = notifEvent.Severity;

            if (template != null)
            {
                var (renderedTitle, renderedBody) = _templateRenderer.Render(template, notifEvent.Parameters, "vi");
                title = renderedTitle;
                body = renderedBody;
                if (string.IsNullOrEmpty(actionUrl) && !string.IsNullOrEmpty(template.ActionUrlPattern))
                {
                    actionUrl = FormatPattern(template.ActionUrlPattern, notifEvent.Parameters);
                }
                if (string.IsNullOrEmpty(severity))
                {
                    severity = template.DefaultSeverity;
                }
            }
            else
            {
                title = notifEvent.Parameters.TryGetValue("title", out var tVal) ? tVal?.ToString() ?? notifEvent.TemplateCode : notifEvent.TemplateCode;
                body = notifEvent.Parameters.TryGetValue("body", out var bVal) ? bVal?.ToString() ?? string.Empty : string.Empty;
                severity = string.IsNullOrEmpty(severity) ? NotificationSeverity.INFO : severity;
            }

            // 3. Resolve Target Recipients
            var recipientUserIds = await router.ResolveRecipientUserIdsAsync(
                notifEvent.TargetType,
                notifEvent.TargetId,
                notifEvent.BranchId,
                notifEvent.CompanyId,
                cancellationToken);

            if (recipientUserIds.Count == 0)
            {
                _logger.LogInformation("No recipients resolved for event {TemplateCode}", notifEvent.TemplateCode);
                return new NotificationResult(true, null, 0);
            }

            // 4. Create Notification Entity
            var notification = new Notification
            {
                TemplateCode = notifEvent.TemplateCode,
                Category = notifEvent.Category,
                Severity = severity,
                Title = title,
                Body = body,
                ActionUrl = actionUrl,
                DataPayload = new Dictionary<string, object>(notifEvent.Parameters),
                TargetType = notifEvent.TargetType,
                TargetId = notifEvent.TargetId,
                CompanyId = notifEvent.CompanyId,
                BranchId = notifEvent.BranchId,
                SenderUserId = notifEvent.SenderUserId,
                IdempotencyKey = notifEvent.IdempotencyKey
            };

            context.Notifications.Add(notification);
            await context.SaveChangesAsync(cancellationToken);

            // 5. Query user preferences for quiet hours & channel toggles
            var preferences = await context.UserNotificationPreferences
                .AsNoTracking()
                .Where(p => recipientUserIds.Contains(p.UserId) && (p.Category == notifEvent.Category || p.Category == "ALL"))
                .ToListAsync(cancellationToken);

            var recipients = new List<NotificationRecipient>();
            var inAppProvider = channelProviders.FirstOrDefault(p => p.Channel == NotificationChannel.InApp);

            foreach (var userId in recipientUserIds)
            {
                var userInAppPref = preferences.FirstOrDefault(p => p.UserId == userId && p.Channel == NotificationChannel.InApp);
                if (userInAppPref != null && !userInAppPref.IsEnabled)
                {
                    continue; // In-app disabled by user
                }

                var recipient = new NotificationRecipient
                {
                    NotificationId = notification.Id,
                    UserId = userId,
                    IsRead = false,
                    IsArchived = false
                };

                recipients.Add(recipient);
                context.NotificationRecipients.Add(recipient);
            }

            await context.SaveChangesAsync(cancellationToken);

            // 6. Push In-App Real-time Delivery via SignalR
            if (inAppProvider != null)
            {
                foreach (var recipient in recipients)
                {
                    await inAppProvider.SendAsync(notification, recipient, cancellationToken);
                }
            }

            return new NotificationResult(true, notification.Id, recipients.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing notification publish for event {TemplateCode}", notifEvent.TemplateCode);
            return new NotificationResult(false, null, 0, ex.Message);
        }
    }

    public async Task<NotificationResult> BroadcastAsync(BroadcastNotificationRequest request, int? senderUserId = null, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var router = scope.ServiceProvider.GetRequiredService<INotificationRouter>();
            var channelProviders = scope.ServiceProvider.GetServices<INotificationChannelProvider>();

            var recipientUserIds = await router.ResolveRecipientUserIdsAsync(
                request.TargetType,
                request.TargetId,
                request.BranchId,
                request.CompanyId,
                cancellationToken);

            if (recipientUserIds.Count == 0)
            {
                return new NotificationResult(true, null, 0);
            }

            var notification = new Notification
            {
                TemplateCode = "SYS_BROADCAST",
                Category = request.Category,
                Severity = request.Severity,
                Title = request.Title,
                Body = request.Body,
                ActionUrl = request.ActionUrl,
                DataPayload = new Dictionary<string, object>
                {
                    ["title"] = request.Title,
                    ["body"] = request.Body,
                    ["broadcast"] = true
                },
                TargetType = request.TargetType,
                TargetId = request.TargetId,
                CompanyId = request.CompanyId,
                BranchId = request.BranchId,
                SenderUserId = senderUserId
            };

            context.Notifications.Add(notification);
            await context.SaveChangesAsync(cancellationToken);

            var recipients = new List<NotificationRecipient>();
            foreach (var userId in recipientUserIds)
            {
                var recipient = new NotificationRecipient
                {
                    NotificationId = notification.Id,
                    UserId = userId,
                    IsRead = false,
                    IsArchived = false
                };
                recipients.Add(recipient);
                context.NotificationRecipients.Add(recipient);
            }

            await context.SaveChangesAsync(cancellationToken);

            var inAppProvider = channelProviders.FirstOrDefault(p => p.Channel == NotificationChannel.InApp);
            if (inAppProvider != null)
            {
                foreach (var recipient in recipients)
                {
                    await inAppProvider.SendAsync(notification, recipient, cancellationToken);
                }
            }

            return new NotificationResult(true, notification.Id, recipients.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting notification");
            return new NotificationResult(false, null, 0, ex.Message);
        }
    }

    private static string FormatPattern(string pattern, IDictionary<string, object> parameters)
    {
        foreach (var (k, v) in parameters)
        {
            pattern = pattern.Replace($"{{{k}}}", v?.ToString() ?? string.Empty);
        }
        return pattern;
    }
}
