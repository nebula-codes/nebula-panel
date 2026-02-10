using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Domain.Interfaces;

/// <summary>
/// Factory that returns the appropriate IServerExecutor for a given game server.
/// If the server has a NodeId, returns a remote executor that delegates to the Agent via gRPC.
/// If NodeId is null, returns a local executor (same as IServerExecutorFactory).
/// </summary>
public interface INodeAwareExecutorFactory
{
    IServerExecutor GetExecutor(GameServer server);
    Task PopulateCacheAsync(CancellationToken cancellationToken = default);
}
