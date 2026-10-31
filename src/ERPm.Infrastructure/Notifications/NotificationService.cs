using ERPm.Application.Common.Notifications;
using ERPm.Application.DTOs.System;
using ERPm.Domain.Entities.System;
using ERPm.Domain.Enums;
using ERPm.Infrastructure.Hubs;
using ERPm.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ERPm.Infrastructure.Notifications;

/// <summary>
/// Implements data queries and state management for client notifications and user preferences.
/// </summary>
public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        ApplicationDbContext context,
        IHubContext<NotificationHub> hubContext,
        ILogger<NotificationService> logger)
    {
        _context = context;
        _hubContext = hubContext;
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

        recipient.IsRead = true;
        recipient.ReadAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        // Notify client tabs of read synchronization
        await _hubContext.Clients.Group($"User_{userId}").SendAsync("NotificationReadSync", new { RecipientId = recipientId }, cancellationToken);

        return true;
    }

    public async Task<int> MarkAllAsReadAsync(int userId, CancellationToken cancellationToken = default)
    {
        var unread = await _context.NotificationRecipients
            .Where(r => r.UserId == userId && !r.IsRead && !r.IsDeleted)
            .ToListAsync(cancellationToken);

        if (unread.Count == 0) return 0;

        var now = DateTime.UtcNow;
        foreach (var r in unread)
        {
            r.IsRead = true;
            r.ReadAtUtc = now;
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Notify client tabs
        await _hubContext.Clients.Group($"User_{userId}").SendAsync("NotificationAllReadSync", cancellationToken);

        return unread.Count;
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
}
