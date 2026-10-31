using ERPm.Application.DTOs.System;

namespace ERPm.Application.Common.Notifications;

/// <summary>
/// Service powering RESTful notification endpoints for web and mobile clients.
/// </summary>
public interface INotificationService
{
    Task<NotificationListResponse> GetNotificationsAsync(
        int userId,
        string? category = null,
        string? severity = null,
        bool? isRead = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<UnreadCountDto> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default);

    Task<bool> MarkAsReadAsync(int userId, int recipientId, CancellationToken cancellationToken = default);

    Task<int> MarkAllAsReadAsync(int userId, CancellationToken cancellationToken = default);

    Task<bool> ArchiveAsync(int userId, int recipientId, CancellationToken cancellationToken = default);

    Task<List<UserNotificationPreferenceDto>> GetPreferencesAsync(int userId, CancellationToken cancellationToken = default);

    Task<UserNotificationPreferenceDto> UpdatePreferenceAsync(int userId, UpdatePreferenceRequest request, CancellationToken cancellationToken = default);
}
