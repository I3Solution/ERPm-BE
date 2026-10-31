using ERPm.Application.Common.Notifications;
using ERPm.Application.DTOs.System;
using ERPm.Domain.Entities.System;
using ERPm.Domain.Enums;
using ERPm.Infrastructure.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace ERPm.Infrastructure.Notifications.Channels;

/// <summary>
/// Dispatches real-time in-app notifications directly via SignalR Hub to connected clients.
/// </summary>
public class InAppChannelProvider : INotificationChannelProvider
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<InAppChannelProvider> _logger;

    public InAppChannelProvider(IHubContext<NotificationHub> hubContext, ILogger<InAppChannelProvider> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public string Channel => NotificationChannel.InApp;

    public async Task SendAsync(Notification notification, NotificationRecipient recipient, CancellationToken cancellationToken = default)
    {
        try
        {
            var dto = new NotificationDto
            {
                Id = recipient.Id,
                ExternalId = recipient.ExternalId,
                NotificationId = notification.Id,
                TemplateCode = notification.TemplateCode,
                Category = notification.Category,
                Severity = notification.Severity,
                Title = notification.Title,
                Body = notification.Body,
                ActionUrl = notification.ActionUrl,
                DataPayload = notification.DataPayload,
                CreatedAtUtc = notification.CreatedAtUtc,
                IsRead = recipient.IsRead,
                ReadAtUtc = recipient.ReadAtUtc,
                IsArchived = recipient.IsArchived
            };

            await _hubContext.Clients
                .Group($"User_{recipient.UserId}")
                .SendAsync("ReceiveNotification", dto, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to push in-app notification to user {UserId} via SignalR", recipient.UserId);
        }
    }
}
