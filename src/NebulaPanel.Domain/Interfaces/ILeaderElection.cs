namespace NebulaPanel.Domain.Interfaces;

/// <summary>
/// Provides distributed leader election for background services.
/// In single-node deployments, always returns true (this node is always leader).
/// In multi-node deployments, uses a database lock to ensure only one node runs
/// singleton services like ResourceUsageCollector, SystemHealthBroadcaster, etc.
/// </summary>
public interface ILeaderElection
{
    /// <summary>
    /// Attempts to acquire or renew leadership for the given lock name.
    /// Returns true if this node holds the lock.
    /// </summary>
    Task<bool> TryAcquireLeadershipAsync(string lockName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases leadership for the given lock name.
    /// </summary>
    Task ReleaseLeadershipAsync(string lockName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if this node currently holds the given lock.
    /// </summary>
    Task<bool> IsLeaderAsync(string lockName, CancellationToken cancellationToken = default);
}
