using ERPm.Application.DTOs.System;
using ERPm.Domain.Events;

namespace ERPm.Application.Common.Notifications;

/// <summary>
/// Central pipeline entry point for publishing notifications system-wide.
/// </summary>
public interface INotificationEngine
{
    Task<NotificationResult> PublishAsync(INotificationEvent notifEvent, CancellationToken cancellationToken = default);
    Task<NotificationResult> BroadcastAsync(BroadcastNotificationRequest request, int? senderUserId = null, CancellationToken cancellationToken = default);
}
