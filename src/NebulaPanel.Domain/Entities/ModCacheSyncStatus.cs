using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;

namespace NebulaPanel.Domain.Entities;

public class ModCacheSyncStatus
{
    public Guid Id { get; set; }
    public ModProviderType Provider { get; set; }
    public ModContentType ContentType { get; set; }
    public ModCacheSyncState State { get; set; }
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public int ItemsSynced { get; set; }
    public int TotalItems { get; set; }
    public DateTime? LastFullSyncStartedAt { get; set; }
    public DateTime? LastFullSyncCompletedAt { get; set; }
    public DateTime? LastIncrementalSyncAt { get; set; }
    public string? LastError { get; set; }
    public DateTime UpdatedAt { get; set; }
}
