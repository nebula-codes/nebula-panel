using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NebulaPanel.Agent.Shared.Mappers;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Infrastructure.Executors;

namespace NebulaPanel.Infrastructure.Services;

/// <summary>
/// Background service that periodically heartbeats all agent nodes.
/// Updates node status, syncs runtime server state (ProcessId, DockerContainerId, Status).
/// Only runs on the leader node.
/// </summary>
public class NodeHeartbeatService : BackgroundService
{
    private const string LeaderLockName = "node-heartbeat";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AgentChannelManager _channelManager;
    private readonly NodeAwareExecutorFactory _executorFactory;
    private readonly ILeaderElection _leaderElection;
    private readonly ILogger<NodeHeartbeatService> _logger;
    private readonly TimeSpan _heartbeatInterval = TimeSpan.FromSeconds(30);
    private readonly ConcurrentDictionary<Guid, int> _consecutiveFailures = new();

    public NodeHeartbeatService(
        IServiceScopeFactory scopeFactory,
        AgentChannelManager channelManager,
        NodeAwareExecutorFactory executorFactory,
        ILeaderElection leaderElection,
        ILogger<NodeHeartbeatService> logger)
    {
        _scopeFactory = scopeFactory;
        _channelManager = channelManager;
        _executorFactory = executorFactory;
        _leaderElection = leaderElection;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NodeHeartbeatService started");

        // Pre-populate the node cache so GetExecutor() never needs a sync DB fallback
        try
        {
            await _executorFactory.PopulateCacheAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to pre-populate node cache on startup");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await _leaderElection.TryAcquireLeadershipAsync(LeaderLockName, stoppingToken).ConfigureAwait(false))
                {
                    await Task.Delay(_heartbeatInterval, stoppingToken).ConfigureAwait(false);
                    continue;
                }

                await HeartbeatAllNodesAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in NodeHeartbeatService");
            }

            try
            {
                await Task.Delay(_heartbeatInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        await _leaderElection.ReleaseLeadershipAsync(LeaderLockName).ConfigureAwait(false);
        _logger.LogInformation("NodeHeartbeatService stopped");
    }

    private async Task HeartbeatAllNodesAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var nodeRepository = scope.ServiceProvider.GetRequiredService<INodeRepository>();
        var serverRepository = scope.ServiceProvider.GetRequiredService<IGameServerRepository>();

        var nodes = await nodeRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);

        foreach (var node in nodes)
        {
            if (node.Status == NodeStatus.Offline && !_consecutiveFailures.ContainsKey(node.Id))
            {
                // Skip nodes that are already offline and haven't been retried
                continue;
            }

            try
            {
                var client = _channelManager.GetClient(node);
                var callOptions = AgentChannelManager.CreateCallOptions(node, cancellationToken);

                var response = await client.HeartbeatAsync(
                    new Agent.Shared.Protos.HeartbeatRequest { NodeId = node.Id.ToString() },
                    callOptions).ConfigureAwait(false);

                // Success — update node
                node.LastHeartbeat = DateTime.UtcNow;
                node.Status = node.Status == NodeStatus.Draining ? NodeStatus.Draining : NodeStatus.Online;
                await nodeRepository.UpdateAsync(node, cancellationToken).ConfigureAwait(false);

                // Update executor factory cache
                _executorFactory.UpdateNodeCache(node);

                // Reset failure counter
                _consecutiveFailures.TryRemove(node.Id, out _);

                // Sync runtime state from heartbeat response
                if (response.RunningServers.Count > 0)
                {
                    await SyncRuntimeStateAsync(
                        serverRepository, node.Id, response.RunningServers, cancellationToken)
                        .ConfigureAwait(false);
                }

                _logger.LogDebug("Heartbeat OK for node {NodeName} ({NodeId})", node.Name, node.Id);
            }
            catch (Exception ex)
            {
                var failures = _consecutiveFailures.AddOrUpdate(node.Id, 1, (_, count) => count + 1);

                _logger.LogWarning(ex, "Heartbeat failed for node {NodeName} ({NodeId}), consecutive failures: {Failures}",
                    node.Name, node.Id, failures);

                if (failures >= 3 && node.Status == NodeStatus.Online)
                {
                    _logger.LogWarning("Marking node {NodeName} as Offline after {Failures} consecutive failures",
                        node.Name, failures);

                    node.Status = NodeStatus.Offline;
                    await nodeRepository.UpdateAsync(node, cancellationToken).ConfigureAwait(false);

                    // Invalidate gRPC channel
                    _channelManager.InvalidateChannel(node.Address);
                    _executorFactory.UpdateNodeCache(node);

                    // Mark servers on this node as Stopped
                    await MarkNodeServersStoppedAsync(serverRepository, node.Id, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }
    }

    private async Task SyncRuntimeStateAsync(
        IGameServerRepository serverRepository,
        Guid nodeId,
        IEnumerable<Agent.Shared.Protos.RunningServerInfo> runningServers,
        CancellationToken cancellationToken)
    {
        foreach (var info in runningServers)
        {
            if (!Guid.TryParse(info.ServerId, out var serverId))
                continue;

            try
            {
                var server = await serverRepository.GetByIdAsync(serverId, cancellationToken).ConfigureAwait(false);
                if (server is null || server.NodeId != nodeId)
                    continue;

                var remoteStatus = ServerConfigMapper.ToDomainStatus(info.Status);
                bool changed = false;

                if (server.Status != remoteStatus)
                {
                    server.Status = remoteStatus;
                    changed = true;
                }

                if (info.ProcessId is not null && server.ProcessId != info.ProcessId)
                {
                    server.ProcessId = info.ProcessId;
                    changed = true;
                }

                if (info.DockerContainerId is not null && server.DockerContainerId != info.DockerContainerId)
                {
                    server.DockerContainerId = info.DockerContainerId;
                    changed = true;
                }

                if (changed)
                {
                    await serverRepository.UpdateAsync(server, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync runtime state for server {ServerId} on node {NodeId}",
                    info.ServerId, nodeId);
            }
        }
    }

    private async Task MarkNodeServersStoppedAsync(
        IGameServerRepository serverRepository,
        Guid nodeId,
        CancellationToken cancellationToken)
    {
        var nodeServers = await serverRepository.GetByNodeIdAndStatusAsync(nodeId, ServerStatus.Running, cancellationToken).ConfigureAwait(false);

        foreach (var server in nodeServers)
        {
            try
            {
                server.Status = ServerStatus.Stopped;
                server.LastStopped = DateTime.UtcNow;
                await serverRepository.UpdateAsync(server, cancellationToken).ConfigureAwait(false);

                _logger.LogWarning("Marked server {ServerName} as Stopped (node {NodeId} went offline)",
                    server.Name, nodeId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to mark server {ServerId} as Stopped after node went offline",
                    server.Id);
            }
        }
    }
}
