namespace NebulaPanel.Domain.ValueObjects;

/// <summary>
/// Represents progress during server update.
/// </summary>
public record UpdateProgress
{
    public double PercentComplete { get; init; }
    public string CurrentStep { get; init; } = string.Empty;
    public string? CurrentFile { get; init; }
    public long BytesDownloaded { get; init; }
    public long TotalBytes { get; init; }
    public bool IsIndeterminate { get; init; }
    public string? FromVersion { get; init; }
    public string? ToVersion { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public static UpdateProgress CheckingForUpdates() => new()
    {
        CurrentStep = "Checking for updates...",
        IsIndeterminate = true
    };

    public static UpdateProgress Downloading(string file, long downloaded, long total) => new()
    {
        PercentComplete = total > 0 ? (downloaded * 100.0 / total) : 0,
        CurrentStep = "Downloading update...",
        CurrentFile = file,
        BytesDownloaded = downloaded,
        TotalBytes = total
    };

    public static UpdateProgress Applying() => new()
    {
        CurrentStep = "Applying update...",
        IsIndeterminate = true
    };

    public static UpdateProgress Completed(string? fromVersion = null, string? toVersion = null) => new()
    {
        PercentComplete = 100,
        CurrentStep = "Update complete",
        FromVersion = fromVersion,
        ToVersion = toVersion
    };
}
