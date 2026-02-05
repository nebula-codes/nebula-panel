namespace NebulaPanel.Domain.ValueObjects;

/// <summary>
/// Progress information for official game server installation/update operations.
/// </summary>
public record OfficialInstallProgress
{
    /// <summary>
    /// Current phase of the operation (e.g., "Downloading", "Extracting", "Configuring").
    /// </summary>
    public required string Phase { get; init; }

    /// <summary>
    /// Human-readable progress message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    public double Percentage { get; init; }

    /// <summary>
    /// Bytes downloaded so far (for download operations).
    /// </summary>
    public long? BytesDownloaded { get; init; }

    /// <summary>
    /// Total bytes to download (for download operations).
    /// </summary>
    public long? TotalBytes { get; init; }

    /// <summary>
    /// When this progress update was created.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a starting progress for a phase.
    /// </summary>
    public static OfficialInstallProgress Starting(string phase) => new()
    {
        Phase = phase,
        Message = $"{phase}...",
        Percentage = 0
    };

    /// <summary>
    /// Creates a progress update with a custom message.
    /// </summary>
    public static OfficialInstallProgress Progress(string phase, string message, double percentage) => new()
    {
        Phase = phase,
        Message = message,
        Percentage = percentage
    };

    /// <summary>
    /// Creates a download progress update.
    /// </summary>
    public static OfficialInstallProgress Downloading(long downloaded, long total) => new()
    {
        Phase = "Downloading",
        Message = $"Downloaded {downloaded / 1024 / 1024}MB of {total / 1024 / 1024}MB",
        Percentage = total > 0 ? downloaded * 100.0 / total : 0,
        BytesDownloaded = downloaded,
        TotalBytes = total
    };

    /// <summary>
    /// Creates a completed progress.
    /// </summary>
    public static OfficialInstallProgress Completed() => new()
    {
        Phase = "Complete",
        Message = "Installation complete",
        Percentage = 100
    };
}
