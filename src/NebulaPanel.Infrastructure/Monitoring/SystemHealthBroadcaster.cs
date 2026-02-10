using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Interfaces;

namespace NebulaPanel.Infrastructure.Monitoring;

/// <summary>
/// Background service that periodically broadcasts system health metrics
/// to connected dashboard clients via SignalR.
/// </summary>
public class SystemHealthBroadcaster : BackgroundService
{
    private const string LeaderLockName = "system-health-broadcaster";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILeaderElection _leaderElection;
    private readonly ILogger<SystemHealthBroadcaster> _logger;
    private readonly TimeSpan _broadcastInterval = TimeSpan.FromSeconds(5);

    public SystemHealthBroadcaster(
        IServiceScopeFactory scopeFactory,
        ILeaderElection leaderElection,
        ILogger<SystemHealthBroadcaster> logger)
    {
        _scopeFactory = scopeFactory;
        _leaderElection = leaderElection;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SystemHealthBroadcaster started with {Interval}s interval", _broadcastInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Only run on the leader node
                if (!await _leaderElection.TryAcquireLeadershipAsync(LeaderLockName, stoppingToken).ConfigureAwait(false))
                {
                    await Task.Delay(_broadcastInterval, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                await BroadcastHealthAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting system health");
            }

            try
            {
                await Task.Delay(_broadcastInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        await _leaderElection.ReleaseLeadershipAsync(LeaderLockName).ConfigureAwait(false);
        _logger.LogInformation("SystemHealthBroadcaster stopped");
    }

    private async Task BroadcastHealthAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var hostMonitorService = scope.ServiceProvider.GetRequiredService<IHostMonitorService>();
        var dashboardNotifier = scope.ServiceProvider.GetService<ISystemHealthNotifier>();

        if (dashboardNotifier is null)
        {
            _logger.LogDebug("DashboardNotifier not available, skipping broadcast");
            return;
        }

        var health = await hostMonitorService.GetCurrentHealthAsync(cancellationToken).ConfigureAwait(false);
        await dashboardNotifier.NotifySystemHealthUpdatedAsync(health).ConfigureAwait(false);
    }
}
