using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Domain.ValueObjects;

/// <summary>
/// Represents progress information during a panel update operation.
/// </summary>
public record PanelUpdateProgress
{
    /// <summary>
    /// The current phase of the update operation.
    /// </summary>
    public UpdateProgressPhase Phase { get; init; }

    /// <summary>
    /// The completion percentage (0-100).
    /// </summary>
    public double Percentage { get; init; }

    /// <summary>
    /// A human-readable message describing the current progress.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// The number of bytes downloaded so far, or null if not applicable.
    /// </summary>
    public long? BytesDownloaded { get; init; }

    /// <summary>
    /// The total number of bytes to download, or null if unknown.
    /// </summary>
    public long? TotalBytes { get; init; }

    /// <summary>
    /// When this progress update was generated.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a progress update for the preparing phase.
    /// </summary>
    public static PanelUpdateProgress Preparing(string message = "Preparing update...") => new()
    {
        Phase = UpdateProgressPhase.Preparing,
        Message = message,
        Percentage = 0
    };

    /// <summary>
    /// Creates a progress update for downloading.
    /// </summary>
    public static PanelUpdateProgress Downloading(long downloaded, long total) => new()
    {
        Phase = UpdateProgressPhase.Downloading,
        Percentage = total > 0 ? downloaded * 100.0 / total : 0,
        Message = $"Downloading... {downloaded / 1024 / 1024}MB / {total / 1024 / 1024}MB",
        BytesDownloaded = downloaded,
        TotalBytes = total
    };

    /// <summary>
    /// Creates a progress update for the verifying phase.
    /// </summary>
    public static PanelUpdateProgress Verifying() => new()
    {
        Phase = UpdateProgressPhase.Verifying,
        Message = "Verifying download integrity...",
        Percentage = 100
    };

    /// <summary>
    /// Creates a progress update for the backup creation phase.
    /// </summary>
    public static PanelUpdateProgress CreatingBackup() => new()
    {
        Phase = UpdateProgressPhase.CreatingBackup,
        Message = "Creating backup...",
        Percentage = 0
    };

    /// <summary>
    /// Creates a progress update for stopping servers.
    /// </summary>
    public static PanelUpdateProgress StoppingServers() => new()
    {
        Phase = UpdateProgressPhase.StoppingServers,
        Message = "Stopping game servers...",
        Percentage = 0
    };

    /// <summary>
    /// Creates a progress update for extracting files.
    /// </summary>
    public static PanelUpdateProgress ExtractingFiles() => new()
    {
        Phase = UpdateProgressPhase.ExtractingFiles,
        Message = "Extracting update files...",
        Percentage = 0
    };

    /// <summary>
    /// Creates a progress update for completing the update.
    /// </summary>
    public static PanelUpdateProgress Completing() => new()
    {
        Phase = UpdateProgressPhase.Completing,
        Message = "Update complete. Restarting...",
        Percentage = 100
    };
}
