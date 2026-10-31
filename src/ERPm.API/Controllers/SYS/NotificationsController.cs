using System.Security.Claims;
using ERPm.API.Security;
using ERPm.Application.Common;
using ERPm.Application.Common.Interfaces;
using ERPm.Application.Common.Notifications;
using ERPm.Application.DTOs.System;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers.SYS;

/// <summary>
/// Enterprise notification controller providing RESTful endpoints for notification lists,
/// unread counters, read receipts, delivery preferences, and system broadcasts.
/// </summary>
[Route("api/v1/sys/notifications")]
public class NotificationsController : BaseApiController
{
    private readonly INotificationService _notificationService;
    private readonly INotificationEngine _notificationEngine;
    private readonly ICurrentUser _currentUser;

    public NotificationsController(
        INotificationService notificationService,
        INotificationEngine notificationEngine,
        ICurrentUser currentUser)
    {
        _notificationService = notificationService;
        _notificationEngine = notificationEngine;
        _currentUser = currentUser;
    }

    private int GetCurrentUserId()
    {
        var claimVal = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? _currentUser.UserId;
        if (int.TryParse(claimVal, out var id)) return id;
        throw new BusinessException("error.auth.unauthorized", 401);
    }

    /// <summary>
    /// Gets paginated notifications for the current authenticated user with optional filtering.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<NotificationListResponse>>> GetNotifications(
        [FromQuery] string? category = null,
        [FromQuery] string? severity = null,
        [FromQuery] bool? isRead = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var result = await _notificationService.GetNotificationsAsync(
            userId, category, severity, isRead, pageIndex, pageSize, cancellationToken);

        return Ok(ApiResponse<NotificationListResponse>.Ok(result));
    }

    /// <summary>
    /// Gets the count of unread notifications for the header bell badge.
    /// </summary>
    [HttpGet("unread-count")]
    public async Task<ActionResult<ApiResponse<UnreadCountDto>>> GetUnreadCount(CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var result = await _notificationService.GetUnreadCountAsync(userId, cancellationToken);
        return Ok(ApiResponse<UnreadCountDto>.Ok(result));
    }

    /// <summary>
    /// Marks an individual notification as read.
    /// </summary>
    [HttpPut("{id:int}/read")]
    public async Task<ActionResult<ApiResponse<bool>>> MarkAsRead(int id, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var success = await _notificationService.MarkAsReadAsync(userId, id, cancellationToken);
        return success
            ? Ok(ApiResponse<bool>.Ok(true, "common.saved"))
            : NotFound(ApiResponse<bool>.Fail("error.validation.reference"));
    }

    /// <summary>
    /// Marks all unread notifications as read for the current user.
    /// </summary>
    [HttpPut("read-all")]
    public async Task<ActionResult<ApiResponse<int>>> MarkAllAsRead(CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var count = await _notificationService.MarkAllAsReadAsync(userId, cancellationToken);
        return Ok(ApiResponse<int>.Ok(count, "common.saved"));
    }

    /// <summary>
    /// Archives a notification item so it no longer appears in the active inbox.
    /// </summary>
    [HttpPut("{id:int}/archive")]
    public async Task<ActionResult<ApiResponse<bool>>> Archive(int id, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var success = await _notificationService.ArchiveAsync(userId, id, cancellationToken);
        return success
            ? Ok(ApiResponse<bool>.Ok(true, "common.saved"))
            : NotFound(ApiResponse<bool>.Fail("error.validation.reference"));
    }

    /// <summary>
    /// Gets the user's notification channel delivery preferences.
    /// </summary>
    [HttpGet("preferences")]
    public async Task<ActionResult<ApiResponse<List<UserNotificationPreferenceDto>>>> GetPreferences(CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var result = await _notificationService.GetPreferencesAsync(userId, cancellationToken);
        return Ok(ApiResponse<List<UserNotificationPreferenceDto>>.Ok(result));
    }

    /// <summary>
    /// Updates a notification channel delivery preference.
    /// </summary>
    [HttpPut("preferences")]
    public async Task<ActionResult<ApiResponse<UserNotificationPreferenceDto>>> UpdatePreference(
        [FromBody] UpdatePreferenceRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var result = await _notificationService.UpdatePreferenceAsync(userId, request, cancellationToken);
        return Ok(ApiResponse<UserNotificationPreferenceDto>.Ok(result, "common.saved"));
    }

    /// <summary>
    /// Broadcasts an ad-hoc system announcement to designated targets.
    /// Requires sys:notification:broadcast permission.
    /// </summary>
    [RequirePermission("sys:notification:broadcast")]
    [HttpPost("broadcast")]
    public async Task<ActionResult<ApiResponse<NotificationResult>>> Broadcast(
        [FromBody] BroadcastNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var result = await _notificationEngine.BroadcastAsync(request, userId, cancellationToken);
        return result.Success
            ? Ok(ApiResponse<NotificationResult>.Ok(result, "common.saved"))
            : BadRequest(ApiResponse<NotificationResult>.Fail(result.ErrorMessage ?? "error.notification.broadcast_failed"));
    }
}
