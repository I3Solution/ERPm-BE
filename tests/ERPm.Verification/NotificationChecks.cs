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
            Code = "TEST_TPL",
            Name = "Test Template",
            Category = "WMS",
            DefaultSeverity = "WARNING",
            TitleTemplate = "Low stock: {item_code}",
            BodyTemplate = "Item {item_code} has only {qty} left in {warehouse}",
            Translations = new Dictionary<string, NotificationTemplateTranslation>
            {
                ["vi"] = new() { Title = "Sắp hết hàng: {item_code}", Body = "Mặt hàng {item_code} chỉ còn {qty} tại kho {warehouse}." },
                ["en"] = new() { Title = "Low stock alert: {item_code}", Body = "Item {item_code} has only {qty} remaining in {warehouse}." }
            }
        };

        var sampleParams = new Dictionary<string, object>
        {
            ["item_code"] = "SKU-999",
            ["qty"] = 5,
            ["warehouse"] = "KHO_TONG"
        };

        var renderedVi = renderer.Render(template, sampleParams, "vi");
        check(renderedVi.Title.Contains("SKU-999") && renderedVi.Body.Contains("kho KHO_TONG"), "TemplateRenderer renders localized Vietnamese strings and formats placeholders");

        var renderedEn = renderer.Render(template, sampleParams, "en");
        check(renderedEn.Title.Contains("Low stock alert: SKU-999") && renderedEn.Body.Contains("5 remaining"), "TemplateRenderer renders localized English strings");

        var ev = new NotificationEvent(
            templateCode: "WMS_LOW_STOCK",
            category: NotificationCategory.WMS,
            severity: NotificationSeverity.WARNING,
            targetType: NotificationTargetType.BranchTree,
            branchId: 1,
            parameters: sampleParams
        );
        check(ev.TemplateCode == "WMS_LOW_STOCK" && ev.Severity == "WARNING" && ev.BranchId == 1, "NotificationEvent constructor preserves category, severity, and branchId");
    }

    public static async Task RunDbChecksAsync(Context ctx)
    {
        await using var db = ctx.Db(null);

        var seededTemplates = await db.NotificationTemplates.AsNoTracking().ToListAsync();
        ctx.Check(seededTemplates.Count >= 5, "at least 5 standard notification templates seeded");

        var lowStockTpl = seededTemplates.FirstOrDefault(t => t.Code == "WMS_LOW_STOCK");
        ctx.Check(lowStockTpl != null && lowStockTpl.Category == "WMS", "WMS_LOW_STOCK template exists");

        var newOrderTpl = seededTemplates.FirstOrDefault(t => t.Code == "SD_ORDER_NEW");
        ctx.Check(newOrderTpl != null && newOrderTpl.Category == "SD", "SD_ORDER_NEW template exists");

        var cancelInvoiceTpl = seededTemplates.FirstOrDefault(t => t.Code == "SD_INVOICE_CANCELLED");
        ctx.Check(cancelInvoiceTpl != null && cancelInvoiceTpl.DefaultSeverity == "DANGER", "SD_INVOICE_CANCELLED alert exists");

        var renderer = new TemplateRenderer();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(db);
        services.AddSingleton<ITemplateRenderer>(renderer);
        services.AddScoped<INotificationRouter, NotificationRouter>();
        services.AddScoped<INotificationChannelProvider, InAppChannelProvider>();
        services.AddSingleton<Microsoft.AspNetCore.SignalR.IHubContext<ERPm.Infrastructure.Hubs.NotificationHub>>(new DummyHubContext());

        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var engine = new NotificationEngine(scopeFactory, renderer, NullLogger<NotificationEngine>.Instance);

        var testUser = await db.AppUsers.FirstOrDefaultAsync(u => u.UserName == "test_notification_user");
        if (testUser == null)
        {
            testUser = new AppUser
            {
                UserName = "test_notification_user",
                DisplayName = "Test User for Notifications",
                PasswordHash = "dummy_hash",
                SecurityStamp = Guid.NewGuid().ToString(),
                IsActive = true
            };
            db.AppUsers.Add(testUser);
            await db.SaveChangesAsync();
        }

        var publishEvent = new NotificationEvent(
            templateCode: "SD_ORDER_NEW",
            category: NotificationCategory.SD,
            severity: NotificationSeverity.INFO,
            targetType: NotificationTargetType.User,
            targetId: testUser.Id,
            parameters: new Dictionary<string, object>
            {
                ["order_code"] = "SO-TEST-001",
                ["customer_name"] = "Nguyen Van A",
                ["amount"] = 1500000m
            },
            idempotencyKey: $"VERIFY-TEST-{Guid.NewGuid()}"
        );

        var pubResult = await engine.PublishAsync(publishEvent);
        ctx.Check(pubResult.Success && pubResult.RecipientCount == 1, "notification published successfully to target user");

        var duplicateResult = await engine.PublishAsync(publishEvent);
        ctx.Check(duplicateResult.Success && duplicateResult.RecipientCount == 0, "idempotency key prevents duplicate notification delivery");

        var service = new NotificationService(db, new DummyHubContext(), renderer, engine, NullLogger<NotificationService>.Instance);
        var unread = await service.GetUnreadCountAsync(testUser.Id);
        ctx.Check(unread.UnreadCount >= 1, "unread notification count is greater than zero");

        var list = await service.GetNotificationsAsync(testUser.Id, pageIndex: 1, pageSize: 10);
        ctx.Check(list.Items.Count >= 1 && list.Items.Any(i => i.TemplateCode == "SD_ORDER_NEW"), "notifications list contains published item");

        var firstItem = list.Items.First(i => i.TemplateCode == "SD_ORDER_NEW");
        var markReadOk = await service.MarkAsReadAsync(testUser.Id, firstItem.Id);
        ctx.Check(markReadOk, "notification marked as read");

        await service.MarkAllAsReadAsync(testUser.Id);
        var unreadAfter = await service.GetUnreadCountAsync(testUser.Id);
        ctx.Check(unreadAfter.UnreadCount == 0, "mark all as read sets unread count to 0");

        var pref = await service.UpdatePreferenceAsync(testUser.Id, new UpdatePreferenceRequest
        {
            Category = NotificationCategory.WMS,
            Channel = NotificationChannel.InApp,
            IsEnabled = false
        });
        ctx.Check(!pref.IsEnabled && pref.Category == "WMS", "user preference successfully toggled");

        // Template Management Checks (CRUD, Preview, Test Send)
        var templates = await service.GetTemplatesAsync();
        ctx.Check(templates.Count >= 5, "at least 5 notification templates retrieved via service");

        var newTpl = await service.CreateTemplateAsync(new CreateNotificationTemplateRequest
        {
            Code = $"TEST_ALERT_{Guid.NewGuid():N}"[..20],
            Name = "Custom Test Alert",
            Category = "SYS",
            DefaultSeverity = "WARNING",
            SupportedChannels = ["IN_APP", "EMAIL"],
            TitleTemplate = "Alert for {user_name}",
            BodyTemplate = "Details: {message}",
            Translations = new Dictionary<string, TemplateTranslationDto>
            {
                ["vi"] = new() { Title = "Cảnh báo cho {user_name}", Body = "Chi tiết: {message}" },
                ["en"] = new() { Title = "Alert for {user_name}", Body = "Details: {message}" }
            }
        }, testUser.Id);
        ctx.Check(newTpl.Id > 0 && newTpl.Name == "Custom Test Alert", "custom notification template created");

        var preview = await service.PreviewTemplateAsync(new PreviewNotificationTemplateRequest
        {
            TitleTemplate = newTpl.TitleTemplate,
            BodyTemplate = newTpl.BodyTemplate,
            Translations = newTpl.Translations,
            SampleData = new Dictionary<string, object>
            {
                ["user_name"] = "Admin Test",
                ["message"] = "Server high load"
            }
        });
        ctx.Check(preview.RenderedTranslations["vi"].Title.Contains("Admin Test"), "template preview rendered with sample data");

        var testSendResult = await service.TestSendTemplateAsync(newTpl.Id, testUser.Id, new Dictionary<string, object>
        {
            ["user_name"] = "Admin Test",
            ["message"] = "Sample broadcast"
        });
        ctx.Check(testSendResult.Success, "test send template delivered successfully");

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
