using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NebulaPanel.Application.Services;
using NebulaPanel.Infrastructure.Configuration;

namespace NebulaPanel.Infrastructure.Services;

/// <summary>
/// Background service that periodically checks for updates.
/// </summary>
public class UpdateBackgroundService(
    IServiceProvider serviceProvider,
    IOptions<UpdateConfiguration> config,
    ILogger<UpdateBackgroundService> logger) : BackgroundService
{
    private readonly UpdateConfiguration _config = config.Value;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.Enabled || !_config.AutoCheck)
        {
            logger.LogInformation("Automatic update checking is disabled");
            return;
        }

        var interval = TimeSpan.FromHours(_config.CheckIntervalHours);
        logger.LogInformation(
            "Update background service started. Check interval: {Interval} hours",
            _config.CheckIntervalHours);

        // Initial delay to let the application fully start
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                logger.LogDebug("Running scheduled update check");

                using var scope = serviceProvider.CreateScope();
                var updateService = scope.ServiceProvider.GetRequiredService<IUpdateService>();

                var result = await updateService.CheckForUpdatesAsync(stoppingToken).ConfigureAwait(false);

                if (result.UpdateAvailable && result.LatestVersion is not null)
                {
                    logger.LogInformation(
                        "Update available: {Version} (current: {CurrentVersion})",
                        result.LatestVersion.FullVersion,
                        updateService.CurrentVersion.FullVersion);
                }
                else if (result.Error is not null)
                {
                    logger.LogWarning("Update check failed: {Error}", result.Error);
                }
                else
                {
                    logger.LogDebug("No updates available");
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error during scheduled update check");
            }

            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("Update background service stopped");
    }
}
