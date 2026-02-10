using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.Monitoring;

/// <summary>
/// Background service that periodically collects resource usage from running servers
/// and stores it in the database for historical charts.
/// </summary>
public class ResourceUsageCollector : BackgroundService
{
    private const string LeaderLockName = "resource-usage-collector";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ProcessResourceMonitor _processMonitor;
    private readonly INodeAwareExecutorFactory _nodeExecutorFactory;
    private readonly ILeaderElection _leaderElection;
    private readonly ILogger<ResourceUsageCollector> _logger;
    private readonly TimeSpan _sampleInterval = TimeSpan.FromMinutes(1);
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);
    private readonly TimeSpan _retentionPeriod = TimeSpan.FromDays(7);
    private DateTime _lastCleanup = DateTime.MinValue;

    public ResourceUsageCollector(
        IServiceScopeFactory scopeFactory,
        ProcessResourceMonitor processMonitor,
        INodeAwareExecutorFactory nodeExecutorFactory,
        ILeaderElection leaderElection,
        ILogger<ResourceUsageCollector> logger)
    {
        _scopeFactory = scopeFactory;
        _processMonitor = processMonitor;
        _nodeExecutorFactory = nodeExecutorFactory;
        _leaderElection = leaderElection;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ResourceUsageCollector started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Only run on the leader node (single-node always wins)
                if (!await _leaderElection.TryAcquireLeadershipAsync(LeaderLockName, stoppingToken).ConfigureAwait(false))
                {
                    _logger.LogDebug("Not the leader for {LockName}, skipping collection cycle", LeaderLockName);
                    await Task.Delay(_sampleInterval, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                await CollectResourceUsageAsync(stoppingToken).ConfigureAwait(false);

                // Periodic cleanup
                if (DateTime.UtcNow - _lastCleanup > _cleanupInterval)
                {
                    await CleanupOldDataAsync(stoppingToken).ConfigureAwait(false);
                    _lastCleanup = DateTime.UtcNow;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ResourceUsageCollector");
            }

            try
            {
                await Task.Delay(_sampleInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        await _leaderElection.ReleaseLeadershipAsync(LeaderLockName).ConfigureAwait(false);
        _logger.LogInformation("ResourceUsageCollector stopped");
    }

    private async Task CollectResourceUsageAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var serverRepository = scope.ServiceProvider.GetRequiredService<IGameServerRepository>();
        var historyRepository = scope.ServiceProvider.GetRequiredService<IResourceUsageHistoryRepository>();

        // Get all running servers (local with ProcessId/ContainerId, or remote with NodeId)
        var runningServers = (await serverRepository.GetByStatusAsync(ServerStatus.Running, cancellationToken).ConfigureAwait(false))
            .Where(s => s.ProcessId.HasValue || !string.IsNullOrEmpty(s.DockerContainerId) || s.NodeId.HasValue);

        var usageRecords = new List<ResourceUsageHistory>();
        var timestamp = DateTime.UtcNow;

        foreach (var server in runningServers)
        {
            try
            {
                ResourceUsage? usage = null;

                if (server.NodeId is null &&
                    server.DeploymentType == ServerDeploymentType.Native &&
                    server.ProcessId.HasValue)
                {
                    // Local native server — use direct process monitoring (avoids unnecessary overhead)
                    usage = ProcessResourceMonitor.GetUsageByPid(server.ProcessId.Value);
                }
                else
                {
                    // Remote servers or local Docker — use node-aware executor (routes via gRPC for remote)
                    var executor = _nodeExecutorFactory.GetExecutor(server);
                    usage = await executor.GetResourceUsageAsync(server, cancellationToken).ConfigureAwait(false);
                }

                if (usage is not null && (usage.CpuPercent > 0 || usage.MemoryBytes > 0))
                {
                    usageRecords.Add(new ResourceUsageHistory
                    {
                        Id = Guid.NewGuid(),
                        ServerId = server.Id,
                        Timestamp = timestamp,
                        CpuPercent = usage.CpuPercent,
                        MemoryBytes = usage.MemoryBytes,
                        PlayerCount = null // Would need RCON/query to get this
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error collecting resource usage for server {ServerId}", server.Id);
            }
        }

        if (usageRecords.Count > 0)
        {
            await historyRepository.AddBatchAsync(usageRecords, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Collected resource usage for {Count} servers", usageRecords.Count);

            await CheckAlertThresholdsAsync(scope.ServiceProvider, usageRecords, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task CheckAlertThresholdsAsync(
        IServiceProvider services,
        List<ResourceUsageHistory> usageRecords,
        CancellationToken cancellationToken)
    {
        try
        {
            var alertRuleRepository = services.GetRequiredService<IAlertRuleRepository>();
            var notificationService = services.GetRequiredService<INotificationService>();

            var enabledRules = await alertRuleRepository.GetEnabledAsync(cancellationToken).ConfigureAwait(false);
            if (enabledRules.Count == 0)
                return;

            var now = DateTime.UtcNow;

            foreach (var rule in enabledRules)
            {
                // Cooldown check
                if (rule.LastTriggeredAt.HasValue &&
                    (now - rule.LastTriggeredAt.Value).TotalMinutes < rule.CooldownMinutes)
                    continue;

                // Only CPU and Memory alerts for per-server rules
                if (rule.ResourceType == AlertResourceType.Disk)
                    continue; // Disk alerts are host-level only, handled elsewhere if needed

                if (rule.ServerId.HasValue)
                {
                    var usage = usageRecords.FirstOrDefault(u => u.ServerId == rule.ServerId.Value);
                    if (usage is null)
                        continue;

                    var value = rule.ResourceType switch
                    {
                        AlertResourceType.CPU => usage.CpuPercent,
                        AlertResourceType.Memory => usage.MemoryBytes / (1024.0 * 1024.0), // MB
                        _ => 0
                    };

                    if (!IsThresholdBreached(value, rule.Comparison, rule.Threshold))
                        continue;

                    rule.LastTriggeredAt = now;
                    await alertRuleRepository.UpdateAsync(rule, cancellationToken).ConfigureAwait(false);

                    var serverName = rule.Server?.Name ?? rule.ServerId.Value.ToString();
                    await notificationService.CreateAsync(new CreateNotificationRequest(
                        rule.OwnerId,
                        NotificationType.Warning,
                        $"Alert: {rule.Name}",
                        $"Server '{serverName}' {rule.ResourceType} is {value:F1} (threshold: {rule.Comparison} {rule.Threshold})",
                        ActionUrl: null,
                        RelatedEntityId: rule.ServerId
                    ), cancellationToken).ConfigureAwait(false);

                    _logger.LogWarning(
                        "Alert triggered: {RuleName} for server {ServerId} — {ResourceType} = {Value:F1}",
                        rule.Name, rule.ServerId, rule.ResourceType, value);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking alert thresholds");
        }
    }

    private static bool IsThresholdBreached(double value, AlertComparison comparison, double threshold)
    {
        return comparison switch
        {
            AlertComparison.GreaterThan => value > threshold,
            AlertComparison.LessThan => value < threshold,
            _ => false
        };
    }

    private async Task CleanupOldDataAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var historyRepository = scope.ServiceProvider.GetRequiredService<IResourceUsageHistoryRepository>();
            var activityRepository = scope.ServiceProvider.GetRequiredService<IServerActivityRepository>();

            var threshold = DateTime.UtcNow - _retentionPeriod;

            await historyRepository.DeleteOlderThanAsync(threshold, cancellationToken).ConfigureAwait(false);
            await activityRepository.DeleteOlderThanAsync(threshold, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Cleaned up resource usage and activity data older than {Threshold}", threshold);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during cleanup of old data");
        }
    }
}
