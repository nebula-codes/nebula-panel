namespace NebulaPanel.Domain.Interfaces;

/// <summary>
/// Service for managing SteamCMD operations including installation and game server updates.
/// </summary>
public interface ISteamCmdService
{
    /// <summary>
    /// Gets whether SteamCMD is installed and available.
    /// </summary>
    bool IsInstalled { get; }

    /// <summary>
    /// Gets the path to the SteamCMD executable.
    /// </summary>
    string? SteamCmdPath { get; }

    /// <summary>
    /// Ensures SteamCMD is downloaded and installed.
    /// Downloads automatically if not present.
    /// </summary>
    Task<bool> EnsureInstalledAsync(
        IProgress<SteamCmdProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Installs or updates a Steam game/dedicated server.
    /// </summary>
    /// <param name="appId">The Steam App ID for the game server.</param>
    /// <param name="installDir">The directory to install the server to.</param>
    /// <param name="branch">Optional beta branch name.</param>
    /// <param name="betaPassword">Optional beta branch password.</param>
    /// <param name="validate">Whether to validate existing files.</param>
    /// <param name="progress">Progress reporter for tracking download progress.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the installation/update succeeded.</returns>
    Task<bool> InstallOrUpdateGameAsync(
        string appId,
        string installDir,
        string? branch = null,
        string? betaPassword = null,
        bool validate = true,
        IProgress<SteamCmdProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets information about an installed app.
    /// </summary>
    /// <param name="appId">The Steam App ID.</param>
    /// <param name="installDir">The installation directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>App info if available, null otherwise.</returns>
    Task<SteamAppInfo?> GetAppInfoAsync(
        string appId,
        string installDir,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Downloads a Steam Workshop item using SteamCMD.
    /// </summary>
    /// <param name="appId">The Steam App ID for the game that owns the workshop item.</param>
    /// <param name="workshopItemId">The Workshop item ID to download.</param>
    /// <param name="installDir">The directory to download the Workshop item to.</param>
    /// <param name="progress">Progress reporter for tracking download progress.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing success status and file path or error.</returns>
    Task<WorkshopDownloadResult> DownloadWorkshopItemAsync(
        string appId,
        string workshopItemId,
        string installDir,
        IProgress<SteamCmdProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a Steam Workshop item download operation.
/// </summary>
public record WorkshopDownloadResult(
    bool Success,
    string? FilePath,
    string? Error
);

/// <summary>
/// Progress information for SteamCMD operations.
/// </summary>
public record SteamCmdProgress
{
    public SteamCmdProgressState State { get; init; }
    public double PercentComplete { get; init; }
    public string Message { get; init; } = string.Empty;
    public long BytesDownloaded { get; init; }
    public long TotalBytes { get; init; }
    public string? CurrentFile { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public static SteamCmdProgress Starting() => new()
    {
        State = SteamCmdProgressState.Starting,
        Message = "Starting SteamCMD..."
    };

    public static SteamCmdProgress Downloading(string appId) => new()
    {
        State = SteamCmdProgressState.DownloadingSteamCmd,
        Message = $"Downloading SteamCMD..."
    };

    public static SteamCmdProgress Extracting() => new()
    {
        State = SteamCmdProgressState.Extracting,
        Message = "Extracting SteamCMD..."
    };

    public static SteamCmdProgress Initializing() => new()
    {
        State = SteamCmdProgressState.Initializing,
        Message = "Initializing SteamCMD (first run may take a while)..."
    };

    public static SteamCmdProgress LoggingIn() => new()
    {
        State = SteamCmdProgressState.LoggingIn,
        Message = "Logging in to Steam..."
    };

    public static SteamCmdProgress Validating() => new()
    {
        State = SteamCmdProgressState.Validating,
        Message = "Validating files..."
    };

    public static SteamCmdProgress DownloadingGame(double percent, long downloaded, long total) => new()
    {
        State = SteamCmdProgressState.DownloadingGame,
        PercentComplete = percent,
        Message = $"Downloading... {percent:F1}%",
        BytesDownloaded = downloaded,
        TotalBytes = total
    };

    public static SteamCmdProgress Installing() => new()
    {
        State = SteamCmdProgressState.Installing,
        Message = "Installing files..."
    };

    public static SteamCmdProgress Completed() => new()
    {
        State = SteamCmdProgressState.Completed,
        PercentComplete = 100,
        Message = "Installation complete"
    };

    public static SteamCmdProgress Failed(string error) => new()
    {
        State = SteamCmdProgressState.Failed,
        Message = error
    };
}

/// <summary>
/// State of SteamCMD operation progress.
/// </summary>
public enum SteamCmdProgressState
{
    Starting,
    DownloadingSteamCmd,
    Extracting,
    Initializing,
    LoggingIn,
    Validating,
    DownloadingGame,
    Installing,
    Completed,
    Failed
}

/// <summary>
/// Information about an installed Steam app.
/// </summary>
public record SteamAppInfo
{
    public string AppId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string InstallDir { get; init; } = string.Empty;
    public string? BuildId { get; init; }
    public long SizeOnDisk { get; init; }
    public DateTime? LastUpdated { get; init; }
    public string? Branch { get; init; }
}
