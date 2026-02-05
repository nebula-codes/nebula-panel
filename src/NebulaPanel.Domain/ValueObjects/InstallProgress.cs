namespace NebulaPanel.Domain.ValueObjects;

/// <summary>
/// Represents progress during server installation.
/// </summary>
public record InstallProgress
{
    public double PercentComplete { get; init; }
    public string CurrentStep { get; init; } = string.Empty;
    public string? CurrentFile { get; init; }
    public long BytesDownloaded { get; init; }
    public long TotalBytes { get; init; }
    public bool IsIndeterminate { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public static InstallProgress Starting() => new()
    {
        PercentComplete = 0,
        CurrentStep = "Starting installation...",
        IsIndeterminate = true
    };

    public static InstallProgress Downloading(string file, long downloaded, long total) => new()
    {
        PercentComplete = total > 0 ? (downloaded * 100.0 / total) : 0,
        CurrentStep = "Downloading files...",
        CurrentFile = file,
        BytesDownloaded = downloaded,
        TotalBytes = total
    };

    public static InstallProgress Extracting(string file) => new()
    {
        CurrentStep = "Extracting files...",
        CurrentFile = file,
        IsIndeterminate = true
    };

    public static InstallProgress Completed() => new()
    {
        PercentComplete = 100,
        CurrentStep = "Installation complete"
    };
}
