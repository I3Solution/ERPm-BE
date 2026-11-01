using ERPm.Application.Common;
using ERPm.Application.Common.Notifications;
using ERPm.Application.DTOs.System;
using ERPm.Domain.Entities.System;
using ERPm.Domain.Enums;
using ERPm.Domain.Events;
using ERPm.Infrastructure.Hubs;
using ERPm.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERPm.Infrastructure.Notifications;

/// <summary>
/// Implements data queries, template CRUD, and state management for client notifications and user preferences.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ITemplateRenderer _templateRenderer;
    private readonly INotificationEngine _notificationEngine;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        ApplicationDbContext context,
        IHubContext<NotificationHub> hubContext,
        ITemplateRenderer templateRenderer,
        INotificationEngine notificationEngine,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _hubContext = hubContext;
        _templateRenderer = templateRenderer;
        _notificationEngine = notificationEngine;
        _logger = logger;
    }

    public async Task<NotificationListResponse> GetNotificationsAsync(
        int userId,
        string? category = null,
        string? severity = null,
        bool? isRead = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (pageIndex < 1) pageIndex = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var query = _context.NotificationRecipients
            .AsNoTracking()
            .Where(r => r.UserId == userId && !r.IsArchived && !r.IsDeleted)
            .Include(r => r.Notification)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
        {
            var cat = category.Trim().ToUpperInvariant();
            query = query.Where(r => r.Notification!.Category == cat);
        }

        if (!string.IsNullOrWhiteSpace(severity))
        {
            var sev = severity.Trim().ToUpperInvariant();
            query = query.Where(r => r.Notification!.Severity == sev);
        }

        if (isRead.HasValue)
        {
            query = query.Where(r => r.IsRead == isRead.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var unreadCount = await _context.NotificationRecipients
            .AsNoTracking()
            .Where(r => r.UserId == userId && !r.IsRead && !r.IsArchived && !r.IsDeleted)
            .CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.Notification!.CreatedAtUtc)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new NotificationDto
            {
                Id = r.Id,
                ExternalId = r.ExternalId,
                NotificationId = r.NotificationId,
                TemplateCode = r.Notification!.TemplateCode,
                Category = r.Notification.Category,
                Severity = r.Notification.Severity,
                Title = r.Notification.Title,
                Body = r.Notification.Body,
                ActionUrl = r.Notification.ActionUrl,
                DataPayload = r.Notification.DataPayload,
                CreatedAtUtc = r.Notification.CreatedAtUtc,
                IsRead = r.IsRead,
                ReadAtUtc = r.ReadAtUtc,
                IsArchived = r.IsArchived
            })
            .ToListAsync(cancellationToken);

        return new NotificationListResponse
        {
            Items = items,
            TotalCount = totalCount,
            UnreadCount = unreadCount,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    public async Task<UnreadCountDto> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default)
    {
        var count = await _context.NotificationRecipients
            .AsNoTracking()
            .Where(r => r.UserId == userId && !r.IsRead && !r.IsArchived && !r.IsDeleted)
            .CountAsync(cancellationToken);

        return new UnreadCountDto(count);
    }

    public async Task<bool> MarkAsReadAsync(int userId, int recipientId, CancellationToken cancellationToken = default)
    {
        var recipient = await _context.NotificationRecipients
            .FirstOrDefaultAsync(r => r.Id == recipientId && r.UserId == userId && !r.IsDeleted, cancellationToken);

        if (recipient == null) return false;

        if (!recipient.IsRead)
        {
            recipient.IsRead = true;
            recipient.ReadAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            var unreadCount = await _context.NotificationRecipients
                .AsNoTracking()
                .Where(r => r.UserId == userId && !r.IsRead && !r.IsArchived && !r.IsDeleted)
                .CountAsync(cancellationToken);

            await _hubContext.Clients.Group($"User_{userId}").SendAsync("NotificationReadSync", recipientId, unreadCount, cancellationToken);
        }

        return true;
    }

    public async Task<int> MarkAllAsReadAsync(int userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var unreadItems = await _context.NotificationRecipients
            .Where(r => r.UserId == userId && !r.IsRead && !r.IsArchived && !r.IsDeleted)
            .ToListAsync(cancellationToken);

        if (unreadItems.Count == 0) return 0;

        foreach (var item in unreadItems)
        {
            item.IsRead = true;
            item.ReadAtUtc = now;
        }

        await _context.SaveChangesAsync(cancellationToken);

        await _hubContext.Clients.Group($"User_{userId}").SendAsync("UnreadCountUpdated", 0, cancellationToken);

        return unreadItems.Count;
    }

    public async Task<bool> ArchiveAsync(int userId, int recipientId, CancellationToken cancellationToken = default)
    {
        var recipient = await _context.NotificationRecipients
            .FirstOrDefaultAsync(r => r.Id == recipientId && r.UserId == userId && !r.IsDeleted, cancellationToken);

        if (recipient == null) return false;

        recipient.IsArchived = true;
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<List<UserNotificationPreferenceDto>> GetPreferencesAsync(int userId, CancellationToken cancellationToken = default)
    {
        var preferences = await _context.UserNotificationPreferences
            .AsNoTracking()
            .Where(p => p.UserId == userId && !p.IsDeleted)
            .Select(p => new UserNotificationPreferenceDto
            {
                Id = p.Id,
                Category = p.Category,
                Channel = p.Channel,
                IsEnabled = p.IsEnabled,
                QuietHoursStart = p.QuietHoursStart,
                QuietHoursEnd = p.QuietHoursEnd
            })
            .ToListAsync(cancellationToken);

        if (preferences.Count > 0) return preferences;

        // Default preferences template
        var categories = new[] { NotificationCategory.SYS, NotificationCategory.WMS, NotificationCategory.SD, NotificationCategory.PUR, NotificationCategory.ACC, NotificationCategory.HRM };
        return categories.Select(cat => new UserNotificationPreferenceDto
        {
            Category = cat,
            Channel = NotificationChannel.InApp,
            IsEnabled = true
        }).ToList();
    }

    public async Task<UserNotificationPreferenceDto> UpdatePreferenceAsync(int userId, UpdatePreferenceRequest request, CancellationToken cancellationToken = default)
    {
        var pref = await _context.UserNotificationPreferences
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Category == request.Category && p.Channel == request.Channel && !p.IsDeleted, cancellationToken);

        if (pref == null)
        {
            pref = new UserNotificationPreference
            {
                UserId = userId,
                Category = request.Category,
                Channel = request.Channel,
                IsEnabled = request.IsEnabled,
                QuietHoursStart = request.QuietHoursStart,
                QuietHoursEnd = request.QuietHoursEnd
            };
            _context.UserNotificationPreferences.Add(pref);
        }
        else
        {
            pref.IsEnabled = request.IsEnabled;
            pref.QuietHoursStart = request.QuietHoursStart;
            pref.QuietHoursEnd = request.QuietHoursEnd;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new UserNotificationPreferenceDto
        {
            Id = pref.Id,
            Category = pref.Category,
            Channel = pref.Channel,
            IsEnabled = pref.IsEnabled,
            QuietHoursStart = pref.QuietHoursStart,
            QuietHoursEnd = pref.QuietHoursEnd
        };
    }

    public async Task<List<NotificationTemplateDto>> GetTemplatesAsync(
        string? category = null,
        string? search = null,
        bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.NotificationTemplates
            .AsNoTracking()
            .Where(t => !t.IsDeleted)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
        {
            var cat = category.Trim().ToUpperInvariant();
            query = query.Where(t => t.Category == cat);
        }

        if (isActive.HasValue)
        {
            query = query.Where(t => t.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(t =>
                t.Code.ToLower().Contains(term) ||
                t.Name.ToLower().Contains(term) ||
                t.TitleTemplate.ToLower().Contains(term) ||
                t.BodyTemplate.ToLower().Contains(term));
        }

        var list = await query
            .OrderBy(t => t.Category)
            .ThenBy(t => t.Code)
            .ToListAsync(cancellationToken);

        return list.Select(MapToTemplateDto).ToList();
    }

    public async Task<NotificationTemplateDto?> GetTemplateByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.NotificationTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, cancellationToken);

        return entity == null ? null : MapToTemplateDto(entity);
    }

    public async Task<NotificationTemplateDto> CreateTemplateAsync(
        CreateNotificationTemplateRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedCode))
            throw new BusinessException("error.notification.template_code_required", 400);

        var exists = await _context.NotificationTemplates
            .AnyAsync(t => t.Code == normalizedCode && !t.IsDeleted, cancellationToken);

        if (exists)
            throw new BusinessException("error.notification.template_code_duplicate", 400);

        var translations = new Dictionary<string, NotificationTemplateTranslation>();
        foreach (var kvp in request.Translations)
        {
            translations[kvp.Key.ToLowerInvariant()] = new NotificationTemplateTranslation
            {
                Title = kvp.Value.Title,
                Body = kvp.Value.Body
            };
        }

        var entity = new NotificationTemplate
        {
            Code = normalizedCode,
            Name = request.Name.Trim(),
            Category = request.Category.Trim().ToUpperInvariant(),
            DefaultSeverity = request.DefaultSeverity.Trim().ToUpperInvariant(),
            SupportedChannels = request.SupportedChannels ?? ["IN_APP"],
            TitleTemplate = request.TitleTemplate.Trim(),
            BodyTemplate = request.BodyTemplate.Trim(),
            Translations = translations,
            ActionUrlPattern = string.IsNullOrWhiteSpace(request.ActionUrlPattern) ? null : request.ActionUrlPattern.Trim(),
            IsDigestAllowed = request.IsDigestAllowed,
            DigestWindowSeconds = request.DigestWindowSeconds,
            IsActive = request.IsActive,
            CreatedBy = userId.ToString()
        };

        _context.NotificationTemplates.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return MapToTemplateDto(entity);
    }

    public async Task<NotificationTemplateDto> UpdateTemplateAsync(
        int id,
        UpdateNotificationTemplateRequest request,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.NotificationTemplates
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, cancellationToken);

        if (entity == null)
            throw new BusinessException("error.notification.template_not_found", 404);

        var translations = new Dictionary<string, NotificationTemplateTranslation>();
        foreach (var kvp in request.Translations)
        {
            translations[kvp.Key.ToLowerInvariant()] = new NotificationTemplateTranslation
            {
                Title = kvp.Value.Title,
                Body = kvp.Value.Body
            };
        }

        entity.Name = request.Name.Trim();
        entity.Category = request.Category.Trim().ToUpperInvariant();
        entity.DefaultSeverity = request.DefaultSeverity.Trim().ToUpperInvariant();
        entity.SupportedChannels = request.SupportedChannels ?? ["IN_APP"];
        entity.TitleTemplate = request.TitleTemplate.Trim();
        entity.BodyTemplate = request.BodyTemplate.Trim();
        entity.Translations = translations;
        entity.ActionUrlPattern = string.IsNullOrWhiteSpace(request.ActionUrlPattern) ? null : request.ActionUrlPattern.Trim();
        entity.IsDigestAllowed = request.IsDigestAllowed;
        entity.DigestWindowSeconds = request.DigestWindowSeconds;
        entity.IsActive = request.IsActive;
        entity.LastModifiedBy = userId.ToString();
        entity.LastModifiedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return MapToTemplateDto(entity);
    }

    public Task<PreviewNotificationTemplateResponse> PreviewTemplateAsync(
        PreviewNotificationTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var renderedTitle = _templateRenderer.RenderString(request.TitleTemplate, request.SampleData);
        var renderedBody = _templateRenderer.RenderString(request.BodyTemplate, request.SampleData);

        var renderedTranslations = new Dictionary<string, TemplateTranslationDto>();
        foreach (var kvp in request.Translations)
        {
            renderedTranslations[kvp.Key] = new TemplateTranslationDto
            {
                Title = _templateRenderer.RenderString(kvp.Value.Title, request.SampleData),
                Body = _templateRenderer.RenderString(kvp.Value.Body, request.SampleData)
            };
        }

        var response = new PreviewNotificationTemplateResponse
        {
            RenderedTitleDefault = renderedTitle,
            RenderedBodyDefault = renderedBody,
            RenderedTranslations = renderedTranslations
        };

        return Task.FromResult(response);
    }

    public async Task<NotificationResult> TestSendTemplateAsync(
        int templateId,
        int targetUserId,
        Dictionary<string, object> sampleData,
        CancellationToken cancellationToken = default)
    {
        var template = await _context.NotificationTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == templateId && !t.IsDeleted, cancellationToken);

        if (template == null)
            throw new BusinessException("error.notification.template_not_found", 404);

        var notifEvent = new NotificationEvent(
            templateCode: template.Code,
            category: template.Category,
            severity: template.DefaultSeverity,
            targetType: NotificationTargetType.User,
            targetId: targetUserId,
            parameters: sampleData,
            actionUrl: template.ActionUrlPattern
        );

        return await _notificationEngine.PublishAsync(notifEvent, cancellationToken);
    }

    private static NotificationTemplateDto MapToTemplateDto(NotificationTemplate t)
    {
        var translations = new Dictionary<string, TemplateTranslationDto>();
        if (t.Translations != null)
        {
            foreach (var kvp in t.Translations)
            {
                translations[kvp.Key] = new TemplateTranslationDto
                {
                    Title = kvp.Value.Title,
                    Body = kvp.Value.Body
                };
            }
        }

        return new NotificationTemplateDto
        {
            Id = t.Id,
            ExternalId = t.ExternalId,
            Code = t.Code,
            Name = t.Name,
            Category = t.Category,
            DefaultSeverity = t.DefaultSeverity,
            SupportedChannels = t.SupportedChannels ?? [],
            TitleTemplate = t.TitleTemplate,
            BodyTemplate = t.BodyTemplate,
            Translations = translations,
            ActionUrlPattern = t.ActionUrlPattern,
            IsDigestAllowed = t.IsDigestAllowed,
            DigestWindowSeconds = t.DigestWindowSeconds,
            IsActive = t.IsActive,
            CreatedAtUtc = t.CreatedAtUtc,
            UpdatedAtUtc = t.LastModifiedAtUtc
        };
    }
}
