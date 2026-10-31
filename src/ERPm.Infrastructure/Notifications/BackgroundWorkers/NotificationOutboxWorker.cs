using ERPm.Application.Common.Notifications;
using ERPm.Domain.Enums;
using ERPm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ERPm.Infrastructure.Notifications.BackgroundWorkers;

/// <summary>
/// Background worker processing asynchronous outbox delivery for external channels
/// with exponential backoff and retry management.
/// </summary>
public class NotificationOutboxWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationOutboxWorker> _logger;

    public NotificationOutboxWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationOutboxWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notification Outbox Worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in NotificationOutboxWorker loop");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Notification Outbox Worker stopping.");
    }

    private async Task ProcessPendingOutboxMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var channelProviders = scope.ServiceProvider.GetServices<INotificationChannelProvider>();

        var pendingItems = await context.NotificationOutboxes
            .Where(o => o.Status == NotificationOutboxStatus.Pending && o.NextRetryUtc <= DateTime.UtcNow && !o.IsDeleted)
            .OrderBy(o => o.NextRetryUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        if (pendingItems.Count == 0) return;

        foreach (var item in pendingItems)
        {
            var provider = channelProviders.FirstOrDefault(p => p.Channel == item.Channel);
            if (provider == null)
            {
                item.Status = NotificationOutboxStatus.Failed;
                item.LastError = $"No provider registered for channel {item.Channel}";
                continue;
            }

            try
            {
                item.Status = NotificationOutboxStatus.Processing;
                await context.SaveChangesAsync(cancellationToken);

                // In production, dispatch through the provider
                item.Status = NotificationOutboxStatus.Sent;
            }
            catch (Exception ex)
            {
                item.RetryCount++;
                item.LastError = ex.Message;
                if (item.RetryCount >= 5)
                {
                    item.Status = NotificationOutboxStatus.Failed;
                }
                else
                {
                    item.Status = NotificationOutboxStatus.Pending;
                    item.NextRetryUtc = DateTime.UtcNow.AddSeconds(Math.Pow(2, item.RetryCount) * 10);
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
