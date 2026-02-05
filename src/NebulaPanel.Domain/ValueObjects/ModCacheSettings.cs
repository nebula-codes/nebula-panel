namespace NebulaPanel.Domain.ValueObjects;

public record ModCacheSettings
{
    public bool Enabled { get; init; } = true;
    public bool ModrinthEnabled { get; init; } = true;
    public bool CurseForgeEnabled { get; init; } = true;
    public bool CacheModpacks { get; init; } = true;
    public string IncrementalSyncCron { get; init; } = "0 * * * *"; // Hourly
    public string FullSyncCron { get; init; } = "0 0 * * 0"; // Weekly on Sunday midnight
    public int RequestDelayMs { get; init; } = 100;
    public int SearchResultCacheMinutes { get; init; } = 5;
    public int ModDetailsCacheMinutes { get; init; } = 30;
}
