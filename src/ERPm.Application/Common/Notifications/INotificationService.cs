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

    // Template management methods
    Task<List<NotificationTemplateDto>> GetTemplatesAsync(
        string? category = null,
        string? search = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default);

    Task<NotificationTemplateDto?> GetTemplateByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<NotificationTemplateDto> CreateTemplateAsync(
        CreateNotificationTemplateRequest request,
        int userId,
        CancellationToken cancellationToken = default);

    Task<NotificationTemplateDto> UpdateTemplateAsync(
        int id,
        UpdateNotificationTemplateRequest request,
        int userId,
        CancellationToken cancellationToken = default);

    Task<PreviewNotificationTemplateResponse> PreviewTemplateAsync(
        PreviewNotificationTemplateRequest request,
        CancellationToken cancellationToken = default);

    Task<NotificationResult> TestSendTemplateAsync(
        int templateId,
        int targetUserId,
        Dictionary<string, object> sampleData,
        CancellationToken cancellationToken = default);
}
