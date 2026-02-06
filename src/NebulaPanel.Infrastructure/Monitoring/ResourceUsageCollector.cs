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
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ProcessResourceMonitor _processMonitor;
    private readonly IServerExecutorFactory _executorFactory;
    private readonly ILogger<ResourceUsageCollector> _logger;
    private readonly TimeSpan _sampleInterval = TimeSpan.FromMinutes(1);
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);
    private readonly TimeSpan _retentionPeriod = TimeSpan.FromDays(7);
    private DateTime _lastCleanup = DateTime.MinValue;

    public ResourceUsageCollector(
        IServiceScopeFactory scopeFactory,
        ProcessResourceMonitor processMonitor,
        IServerExecutorFactory executorFactory,
        ILogger<ResourceUsageCollector> logger)
    {
        _scopeFactory = scopeFactory;
        _processMonitor = processMonitor;
        _executorFactory = executorFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ResourceUsageCollector started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
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

        _logger.LogInformation("ResourceUsageCollector stopped");
    }

    private async Task CollectResourceUsageAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var serverRepository = scope.ServiceProvider.GetRequiredService<IGameServerRepository>();
        var historyRepository = scope.ServiceProvider.GetRequiredService<IResourceUsageHistoryRepository>();

        // Get all running servers (native with ProcessId OR Docker with ContainerId)
        var servers = await serverRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var runningServers = servers.Where(s =>
            s.Status == ServerStatus.Running &&
            (s.ProcessId.HasValue || !string.IsNullOrEmpty(s.DockerContainerId)));

        var usageRecords = new List<ResourceUsageHistory>();
        var timestamp = DateTime.UtcNow;

        foreach (var server in runningServers)
        {
            try
            {
                ResourceUsage? usage = null;

                if (server.DeploymentType == ServerDeploymentType.Docker &&
                    !string.IsNullOrEmpty(server.DockerContainerId))
                {
                    // Use Docker stats API via executor
                    var dockerExecutor = _executorFactory.GetExecutor(ServerDeploymentType.Docker);
                    usage = await dockerExecutor.GetResourceUsageAsync(server, cancellationToken).ConfigureAwait(false);
                }
                else if (server.ProcessId.HasValue)
                {
                    // Use process-based monitoring for native servers
                    usage = ProcessResourceMonitor.GetUsageByPid(server.ProcessId.Value);
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
