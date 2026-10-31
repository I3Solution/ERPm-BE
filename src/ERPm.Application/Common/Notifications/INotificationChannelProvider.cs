using ERPm.Domain.Entities.System;

namespace ERPm.Application.Common.Notifications;

/// <summary>
/// Abstraction for a delivery channel provider (In-App SignalR, Email, Push FCM, SMS, Webhook).
/// </summary>
public interface INotificationChannelProvider
{
    string Channel { get; }
    Task SendAsync(Notification notification, NotificationRecipient recipient, CancellationToken cancellationToken = default);
}
