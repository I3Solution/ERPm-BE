using ERPm.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ERPm.Infrastructure.Services.HumanResource;

/// <summary>
/// Runs <see cref="EmployeeSnapshotRefresher"/> once at start-up (catching up after downtime) and then every day
/// shortly after midnight UTC, so a transfer scheduled for a future date reaches the employee profile on its day.
/// </summary>
public class EmployeeSnapshotWorker(IServiceScopeFactory scopeFactory, ILogger<EmployeeSnapshotWorker> logger) : BackgroundService
{
    /// <summary>Time of day (UTC) of the daily run.</summary>
    public static readonly TimeSpan RunAt = new(0, 5, 0);

    /// <summary>How long to wait from <paramref name="now"/> until the next daily run.</summary>
    public static TimeSpan DelayUntilNextRun(DateTime now)
    {
        var next = now.Date + RunAt;
        if (next <= now) next = next.AddDays(1);
        return next - now;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken);
            try { await Task.Delay(DelayUntilNextRun(DateTime.UtcNow), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var refresher = scope.ServiceProvider.GetRequiredService<EmployeeSnapshotRefresher>();
            var fixedCount = await refresher.RefreshStaleAsync(DateOnly.FromDateTime(DateTime.UtcNow), cancellationToken);
            if (fixedCount > 0) logger.LogInformation("Employee snapshot refresh updated {Count} employee(s) from work history.", fixedCount);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            // A failed run must not stop the worker: the next day's run catches up.
            logger.LogError(ex, "Employee snapshot refresh failed.");
        }
    }
}
