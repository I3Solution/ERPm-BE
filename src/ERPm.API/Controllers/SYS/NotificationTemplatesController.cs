using System.Security.Claims;
using ERPm.API.Security;
using ERPm.Application.Common;
using ERPm.Application.Common.Interfaces;
using ERPm.Application.Common.Notifications;
using ERPm.Application.DTOs.System;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers.SYS;

/// <summary>
/// RESTful controller for managing and live-testing enterprise notification templates.
/// </summary>
[Route("api/v1/sys/notification-templates")]
public class NotificationTemplatesController : BaseApiController
{
    private readonly INotificationService _notificationService;
    private readonly ICurrentUser _currentUser;

    public NotificationTemplatesController(
        INotificationService notificationService,
        ICurrentUser currentUser)
    {
        _notificationService = notificationService;
        _currentUser = currentUser;
    }

    private int GetCurrentUserId()
    {
        var claimVal = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? _currentUser.UserId;
        if (int.TryParse(claimVal, out var id)) return id;
        throw new BusinessException("error.auth.unauthorized", 401);
    }

    /// <summary>
    /// Gets all notification templates with optional category, search, and active status filters.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<NotificationTemplateDto>>>> GetTemplates(
        [FromQuery] string? category = null,
        [FromQuery] string? search = null,
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var list = await _notificationService.GetTemplatesAsync(category, search, isActive, cancellationToken);
        return Ok(ApiResponse<List<NotificationTemplateDto>>.Ok(list));
    }

    /// <summary>
    /// Gets an individual notification template by its identifier.
    /// </summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApiResponse<NotificationTemplateDto>>> GetTemplate(
        int id,
        CancellationToken cancellationToken = default)
    {
        var template = await _notificationService.GetTemplateByIdAsync(id, cancellationToken);
        if (template == null)
            return NotFound(ApiResponse<NotificationTemplateDto>.Fail("error.notification.template_not_found"));

        return Ok(ApiResponse<NotificationTemplateDto>.Ok(template));
    }

    /// <summary>
    /// Creates a new notification template.
    /// Requires sys:notification:edit permission.
    /// </summary>
    [RequirePermission("sys:notification:edit")]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<NotificationTemplateDto>>> CreateTemplate(
        [FromBody] CreateNotificationTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var template = await _notificationService.CreateTemplateAsync(request, userId, cancellationToken);
        return Ok(ApiResponse<NotificationTemplateDto>.Ok(template, "common.saved"));
    }

    /// <summary>
    /// Updates an existing notification template.
    /// Requires sys:notification:edit permission.
    /// </summary>
    [RequirePermission("sys:notification:edit")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ApiResponse<NotificationTemplateDto>>> UpdateTemplate(
        int id,
        [FromBody] UpdateNotificationTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var template = await _notificationService.UpdateTemplateAsync(id, request, userId, cancellationToken);
        return Ok(ApiResponse<NotificationTemplateDto>.Ok(template, "common.saved"));
    }

    /// <summary>
    /// Renders dynamic template strings with sample variables for real-time live preview.
    /// </summary>
    [HttpPost("preview")]
    public async Task<ActionResult<ApiResponse<PreviewNotificationTemplateResponse>>> PreviewTemplate(
        [FromBody] PreviewNotificationTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _notificationService.PreviewTemplateAsync(request, cancellationToken);
        return Ok(ApiResponse<PreviewNotificationTemplateResponse>.Ok(result));
    }

    /// <summary>
    /// Sends a real test notification using the specified template to the current authenticated user.
    /// Requires sys:notification:broadcast permission.
    /// </summary>
    [RequirePermission("sys:notification:broadcast")]
    [HttpPost("{id:int}/test-send")]
    public async Task<ActionResult<ApiResponse<NotificationResult>>> TestSend(
        int id,
        [FromBody] TestSendTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        var result = await _notificationService.TestSendTemplateAsync(id, userId, request.SampleData, cancellationToken);

        return result.Success
            ? Ok(ApiResponse<NotificationResult>.Ok(result, "common.saved"))
            : BadRequest(ApiResponse<NotificationResult>.Fail(result.ErrorMessage ?? "error.notification.test_send_failed"));
    }
}
