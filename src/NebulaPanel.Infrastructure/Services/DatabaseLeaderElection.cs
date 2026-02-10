using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Infrastructure.Persistence;

namespace NebulaPanel.Infrastructure.Services;

/// <summary>
/// Database-based leader election using a cluster_locks table.
/// Each lock has an expiry time. If the holder doesn't renew before expiry,
/// another node can take over. In single-node mode, this node always wins.
/// </summary>
public class DatabaseLeaderElection : ILeaderElection
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DatabaseLeaderElection> _logger;
    private readonly string _nodeId;
    private readonly TimeSpan _lockDuration = TimeSpan.FromMinutes(2);

    public DatabaseLeaderElection(
        IServiceScopeFactory scopeFactory,
        ILogger<DatabaseLeaderElection> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _nodeId = Environment.MachineName + "-" + Environment.ProcessId;
    }

    public async Task<bool> TryAcquireLeadershipAsync(string lockName, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<NebulaPanelDbContext>();
            var now = DateTime.UtcNow;

            var existingLock = await context.ClusterLocks
                .FirstOrDefaultAsync(l => l.LockName == lockName, cancellationToken)
                .ConfigureAwait(false);

            if (existingLock is null)
            {
                // No lock exists, create one
                context.ClusterLocks.Add(new ClusterLock
                {
                    LockName = lockName,
                    HolderNodeId = _nodeId,
                    AcquiredAt = now,
                    ExpiresAt = now + _lockDuration
                });

                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Acquired leader lock '{LockName}' for node {NodeId}", lockName, _nodeId);
                return true;
            }

            if (existingLock.HolderNodeId == _nodeId)
            {
                // We already hold this lock, renew it
                existingLock.ExpiresAt = now + _lockDuration;
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return true;
            }

            if (existingLock.ExpiresAt < now)
            {
                // Lock expired, take it over
                existingLock.HolderNodeId = _nodeId;
                existingLock.AcquiredAt = now;
                existingLock.ExpiresAt = now + _lockDuration;
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Took over expired leader lock '{LockName}' from previous holder, now node {NodeId}",
                    lockName, _nodeId);
                return true;
            }

            // Another node holds a valid lock
            return false;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Optimistic concurrency conflict — another node won the race
            return false;
        }
        catch (DbUpdateException)
        {
            // Concurrent insert — another node won the race
            return false;
        }
    }

    public async Task ReleaseLeadershipAsync(string lockName, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<NebulaPanelDbContext>();

            var existingLock = await context.ClusterLocks
                .FirstOrDefaultAsync(l => l.LockName == lockName && l.HolderNodeId == _nodeId, cancellationToken)
                .ConfigureAwait(false);

            if (existingLock is not null)
            {
                context.ClusterLocks.Remove(existingLock);
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Released leader lock '{LockName}' for node {NodeId}", lockName, _nodeId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error releasing leader lock '{LockName}'", lockName);
        }
    }

    public async Task<bool> IsLeaderAsync(string lockName, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<NebulaPanelDbContext>();
            var now = DateTime.UtcNow;

            var existingLock = await context.ClusterLocks
                .FirstOrDefaultAsync(l => l.LockName == lockName, cancellationToken)
                .ConfigureAwait(false);

            return existingLock is not null &&
                   existingLock.HolderNodeId == _nodeId &&
                   existingLock.ExpiresAt > now;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking leader status for lock '{LockName}'", lockName);
            return false;
        }
    }
}
