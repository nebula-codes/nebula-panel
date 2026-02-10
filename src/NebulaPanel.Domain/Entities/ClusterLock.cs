namespace NebulaPanel.Domain.Entities;

/// <summary>
/// Database-based distributed lock for leader election.
/// Only the holder of the lock should run singleton background services.
/// </summary>
public class ClusterLock
{
    public string LockName { get; set; } = string.Empty;
    public string HolderNodeId { get; set; } = string.Empty;
    public DateTime AcquiredAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
