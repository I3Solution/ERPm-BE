using ERPm.Application.Common.Notifications;
using ERPm.Domain.Entities.System;
using ERPm.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ERPm.Infrastructure.Notifications.Channels;

/// <summary>
/// Email delivery channel provider interface implementation.
/// </summary>
public class EmailChannelProvider : INotificationChannelProvider
{
    private readonly ILogger<EmailChannelProvider> _logger;

    public EmailChannelProvider(ILogger<EmailChannelProvider> logger)
    {
        _logger = logger;
    }

    public string Channel => NotificationChannel.Email;

    public Task SendAsync(Notification notification, NotificationRecipient recipient, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Email notification dispatched for user {UserId}: {Title}", recipient.UserId, notification.Title);
        return Task.CompletedTask;
    }
}
