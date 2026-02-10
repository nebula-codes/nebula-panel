using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.Repositories;

namespace NebulaPanel.Infrastructure.Executors;

/// <summary>
/// Routes executor creation based on GameServer.NodeId.
/// Null NodeId = local execution via IServerExecutorFactory.
/// Non-null NodeId = remote execution via RemoteServerExecutor + gRPC.
/// </summary>
public class NodeAwareExecutorFactory(
    IServerExecutorFactory localFactory,
    AgentChannelManager channelManager,
    IServiceScopeFactory scopeFactory,
    ILogger<NodeAwareExecutorFactory> logger) : INodeAwareExecutorFactory
{
    private readonly IServerExecutorFactory _localFactory = localFactory;
    private readonly AgentChannelManager _channelManager = channelManager;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<NodeAwareExecutorFactory> _logger = logger;

    // Cache nodes in memory to avoid sync-over-async; refreshed by NodeHeartbeatService
    private readonly ConcurrentDictionary<Guid, Node> _nodeCache = new();

    public IServerExecutor GetExecutor(GameServer server)
    {
        if (server.NodeId is null)
        {
            return _localFactory.GetExecutor(server.DeploymentType);
        }

        var node = GetNode(server.NodeId.Value);
        var client = _channelManager.GetClient(node);
        var callOptions = AgentChannelManager.CreateCallOptions(node);

        return new RemoteServerExecutor(client, callOptions, _logger);
    }

    /// <summary>
    /// Updates the cached node. Called by NodeHeartbeatService after successful heartbeats.
    /// </summary>
    public void UpdateNodeCache(Node node)
    {
        _nodeCache.AddOrUpdate(node.Id, node, (_, _) => node);
    }

    /// <summary>
    /// Removes a node from the cache.
    /// </summary>
    public void RemoveFromCache(Guid nodeId)
    {
        _nodeCache.TryRemove(nodeId, out _);
    }

    /// <summary>
    /// Loads all nodes into cache. Called at startup by NodeHeartbeatService
    /// before the heartbeat loop begins, so GetNode() never needs a sync DB fallback.
    /// </summary>
    public async Task PopulateCacheAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<INodeRepository>();
        var nodes = await repo.GetAllAsync(cancellationToken).ConfigureAwait(false);

        foreach (var node in nodes)
        {
            _nodeCache.AddOrUpdate(node.Id, node, (_, _) => node);
        }

        _logger.LogInformation("Populated node cache with {Count} node(s)", nodes.Count);
    }

    private Node GetNode(Guid nodeId)
    {
        if (_nodeCache.TryGetValue(nodeId, out var cached))
        {
            return cached;
        }

        throw new InvalidOperationException(
            $"Node {nodeId} not found in cache. Ensure PopulateCacheAsync has been called.");
    }
}
