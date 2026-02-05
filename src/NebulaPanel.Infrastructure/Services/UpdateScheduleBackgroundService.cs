using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Services;

namespace NebulaPanel.Infrastructure.Services;

/// <summary>
/// Background service that processes scheduled updates and sends countdown notifications.
/// </summary>
public class UpdateScheduleBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<UpdateScheduleBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan[] CountdownAlerts =
    [
        TimeSpan.FromMinutes(30),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromSeconds(30)
    ];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Update schedule background service started");

        // Wait a bit before starting to let the app fully initialize
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessSchedulesAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in update schedule background service");
            }

            try
            {
                await Task.Delay(CheckInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Update schedule background service stopped");
    }

    private async Task ProcessSchedulesAsync(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var scheduleService = scope.ServiceProvider.GetRequiredService<IUpdateScheduleService>();
        var notifier = scope.ServiceProvider.GetRequiredService<IUpdateNotifier>();

        // Get the pending schedule
        var pending = await scheduleService.GetPendingScheduleAsync(stoppingToken)
            .ConfigureAwait(false);

        if (pending is null)
            return;

        var timeUntil = pending.ScheduledAt - DateTime.UtcNow;

        // Check if it's time to send a countdown alert
        foreach (var alertTime in CountdownAlerts)
        {
            // If we're within the alert window (within 1 minute of the alert time)
            if (timeUntil > alertTime - TimeSpan.FromSeconds(30) &&
                timeUntil <= alertTime + TimeSpan.FromSeconds(30))
            {
                logger.LogInformation(
                    "Sending countdown notification: {TimeUntil} until update to {Version}",
                    alertTime, pending.TargetVersion);

                await NotifyCountdownAsync(
                    notifier,
                    (int)timeUntil.TotalSeconds,
                    pending.TargetVersion,
                    stoppingToken).ConfigureAwait(false);
                break;
            }
        }

        // Check if it's time to execute
        if (timeUntil <= TimeSpan.Zero)
        {
            logger.LogInformation(
                "Executing scheduled update to version {Version}",
                pending.TargetVersion);

            await scheduleService.ProcessDueSchedulesAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private static async Task NotifyCountdownAsync(
        IUpdateNotifier notifier,
        int secondsRemaining,
        string version,
        CancellationToken cancellationToken)
    {
        await notifier.NotifyUpdateCountdownAsync(secondsRemaining, version, cancellationToken)
            .ConfigureAwait(false);
    }
}
