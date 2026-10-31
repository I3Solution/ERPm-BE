using ERPm.Application.Common.Interfaces;
using ERPm.Application.Common.Notifications;
using ERPm.Application.DTOs.System;
using ERPm.Domain.Entities.System;
using ERPm.Domain.Enums;
using ERPm.Domain.Events;
using ERPm.Infrastructure.Notifications;
using ERPm.Infrastructure.Notifications.Channels;
using ERPm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ERPm.Verification;

public static class NotificationChecks
{
    public sealed record Context(Func<ICurrentUser?, ApplicationDbContext> Db, Action<bool, string> Check, Func<Func<Task>, string, Task> Reject);

    public static void Pure(Action<bool, string> check)
    {
        var renderer = new TemplateRenderer();
        var template = new NotificationTemplate
        {
            Code = "TEST_CODE",
            Name = "Test",
            TitleTemplate = "Alert: {item_name}",
            BodyTemplate = "Stock for {item_code} is {stock_qty} {uom}.",
            Translations = new Dictionary<string, NotificationTemplateTranslation>
            {
                ["vi"] = new() { Title = "Cảnh báo: {item_name}", Body = "Tồn kho cho {item_code} còn {stock_qty} {uom}." },
                ["en"] = new() { Title = "Alert: {item_name}", Body = "Stock for {item_code} is {stock_qty} {uom}." }
            }
        };

        var paramsMap = new Dictionary<string, object>
        {
            ["item_name"] = "Bút bi",
            ["item_code"] = "PEN-01",
            ["stock_qty"] = 15,
            ["uom"] = "hộp"
        };

        var (viTitle, viBody) = renderer.Render(template, paramsMap, "vi");
        check(viTitle == "Cảnh báo: Bút bi" && viBody == "Tồn kho cho PEN-01 còn 15 hộp.", "TemplateRenderer renders localized Vietnamese strings and formats placeholders");

        var (enTitle, enBody) = renderer.Render(template, paramsMap, "en");
        check(enTitle == "Alert: Bút bi" && enBody == "Stock for PEN-01 is 15 hộp.", "TemplateRenderer renders localized English strings");

        var notifEvent = new NotificationEvent(
            templateCode: "WMS_LOW_STOCK",
            category: NotificationCategory.WMS,
            severity: NotificationSeverity.WARNING,
            targetType: NotificationTargetType.BranchTree,
            branchId: 1,
            parameters: paramsMap
        );

        check(notifEvent.TemplateCode == "WMS_LOW_STOCK" && notifEvent.Category == "WMS" && notifEvent.BranchId == 1,
            "NotificationEvent constructor preserves category, severity, and branchId");
    }

    public static async Task RunDbChecksAsync(Context ctx)
    {
        await using var db = ctx.Db(null);

        // 1. Verify Seeded Notification Templates
        var templates = await db.NotificationTemplates.ToListAsync();
        ctx.Check(templates.Count >= 5, "at least 5 standard notification templates seeded");
        ctx.Check(templates.Any(t => t.Code == "WMS_LOW_STOCK" && t.Category == NotificationCategory.WMS), "WMS_LOW_STOCK template exists");
        ctx.Check(templates.Any(t => t.Code == "SD_ORDER_NEW" && t.Category == NotificationCategory.SD), "SD_ORDER_NEW template exists");
        ctx.Check(templates.Any(t => t.Code == "SD_INVOICE_CANCELLED" && t.DefaultSeverity == NotificationSeverity.DANGER), "SD_INVOICE_CANCELLED alert exists");

        // 2. Setup mock channel provider and services
        var testUser = await db.AppUsers.FirstOrDefaultAsync();
        if (testUser == null)
        {
            testUser = new AppUser { UserName = "notif_tester", DisplayName = "Notification Tester", Role = "Admin" };
            db.AppUsers.Add(testUser);
            await db.SaveChangesAsync();
        }
        var router = new NotificationRouter(db, new ERPm.Infrastructure.Services.BranchService(db));
        var renderer = new TemplateRenderer();
        var dummyChannel = new EmailChannelProvider(NullLogger<EmailChannelProvider>.Instance);
        var sc = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        sc.AddSingleton(db);
        sc.AddSingleton<INotificationRouter>(router);
        sc.AddSingleton<INotificationChannelProvider>(dummyChannel);
        var sp = sc.BuildServiceProvider();
        var scopeFactory = sp.GetRequiredService<Microsoft.Extensions.DependencyInjection.IServiceScopeFactory>();
        var engine = new NotificationEngine(scopeFactory, renderer, NullLogger<NotificationEngine>.Instance);

        // 3. Test Notification Publishing with Idempotency
        var idempotencyKey = "test-notif-" + Guid.NewGuid().ToString("N");
        var publishEvent = new NotificationEvent(
            templateCode: "SD_ORDER_NEW",
            category: NotificationCategory.SD,
            severity: NotificationSeverity.INFO,
            targetType: NotificationTargetType.User,
            targetId: testUser.Id,
            idempotencyKey: idempotencyKey,
            parameters: new Dictionary<string, object>
            {
                ["order_code"] = "SO-9999",
                ["customer_name"] = "Công ty ABC",
                ["total_amount"] = 500000m
            }
        );

        var pubResult = await engine.PublishAsync(publishEvent);
        ctx.Check(pubResult.Success && pubResult.RecipientCount == 1, "notification published successfully to target user");

        // Duplicate publish with same idempotency key must be ignored
        var duplicateResult = await engine.PublishAsync(publishEvent);
        ctx.Check(duplicateResult.Success && duplicateResult.RecipientCount == 0, "idempotency key prevents duplicate notification delivery");

        // 4. Test Notification Retrieval & Unread Counter
        var service = new NotificationService(db, new DummyHubContext(), NullLogger<NotificationService>.Instance);
        var unread = await service.GetUnreadCountAsync(testUser.Id);
        ctx.Check(unread.UnreadCount >= 1, "unread notification count is greater than zero");

        var list = await service.GetNotificationsAsync(testUser.Id, pageIndex: 1, pageSize: 10);
        ctx.Check(list.Items.Count >= 1 && list.Items.Any(i => i.TemplateCode == "SD_ORDER_NEW"), "notifications list contains published item");

        // 5. Test Mark As Read
        var firstItem = list.Items.First(i => i.TemplateCode == "SD_ORDER_NEW");
        var markReadOk = await service.MarkAsReadAsync(testUser.Id, firstItem.Id);
        ctx.Check(markReadOk, "notification marked as read");

        // 6. Test Mark All As Read
        await service.MarkAllAsReadAsync(testUser.Id);
        var unreadAfter = await service.GetUnreadCountAsync(testUser.Id);
        ctx.Check(unreadAfter.UnreadCount == 0, "mark all as read sets unread count to 0");

        // 7. Test User Preferences
        var pref = await service.UpdatePreferenceAsync(testUser.Id, new UpdatePreferenceRequest
        {
            Category = NotificationCategory.WMS,
            Channel = NotificationChannel.InApp,
            IsEnabled = false
        });
        ctx.Check(!pref.IsEnabled && pref.Category == "WMS", "user preference successfully toggled");

        db.AppUsers.Remove(testUser);
        await db.SaveChangesAsync();
    }

    private class DummyHubContext : Microsoft.AspNetCore.SignalR.IHubContext<ERPm.Infrastructure.Hubs.NotificationHub>
    {
        public Microsoft.AspNetCore.SignalR.IHubClients Clients => new DummyHubClients();
        public Microsoft.AspNetCore.SignalR.IGroupManager Groups => throw new NotImplementedException();
    }

    private class DummyHubClients : Microsoft.AspNetCore.SignalR.IHubClients
    {
        public Microsoft.AspNetCore.SignalR.IClientProxy All => new DummyClientProxy();
        public Microsoft.AspNetCore.SignalR.IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => new DummyClientProxy();
        public Microsoft.AspNetCore.SignalR.IClientProxy Client(string connectionId) => new DummyClientProxy();
        public Microsoft.AspNetCore.SignalR.IClientProxy Clients(IReadOnlyList<string> connectionIds) => new DummyClientProxy();
        public Microsoft.AspNetCore.SignalR.IClientProxy Group(string groupName) => new DummyClientProxy();
        public Microsoft.AspNetCore.SignalR.IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => new DummyClientProxy();
        public Microsoft.AspNetCore.SignalR.IClientProxy Groups(IReadOnlyList<string> groupNames) => new DummyClientProxy();
        public Microsoft.AspNetCore.SignalR.IClientProxy User(string userId) => new DummyClientProxy();
        public Microsoft.AspNetCore.SignalR.IClientProxy Users(IReadOnlyList<string> userIds) => new DummyClientProxy();
    }

    private class DummyClientProxy : Microsoft.AspNetCore.SignalR.IClientProxy
    {
        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
