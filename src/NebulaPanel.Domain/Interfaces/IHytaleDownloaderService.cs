namespace NebulaPanel.Domain.Interfaces;

/// <summary>
/// Service for managing the hytale-downloader CLI tool and downloading Hytale server files.
/// </summary>
public interface IHytaleDownloaderService
{
    /// <summary>
    /// Gets whether the hytale-downloader binary is installed.
    /// </summary>
    bool IsInstalled { get; }

    /// <summary>
    /// Gets the path to the hytale-downloader executable.
    /// </summary>
    string? DownloaderPath { get; }

    /// <summary>
    /// Ensures the hytale-downloader binary is installed and up to date.
    /// Downloads from https://downloader.hytale.com/hytale-downloader.zip if needed.
    /// </summary>
    Task<bool> EnsureInstalledAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the current version of the hytale-downloader tool.
    /// </summary>
    Task<string> GetDownloaderVersionAsync(CancellationToken ct = default);

    /// <summary>
    /// Checks if a newer version of hytale-downloader is available.
    /// </summary>
    Task<HytaleUpdateCheckResult> CheckDownloaderUpdateAsync(CancellationToken ct = default);

    /// <summary>
    /// Updates the hytale-downloader to the latest version.
    /// </summary>
    Task<bool> UpdateDownloaderAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the latest available Hytale server version.
    /// </summary>
    /// <param name="patchline">The patchline to check (e.g., "release", "pre-release").</param>
    /// <param name="credentials">Optional credentials for authenticated requests.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Version info if successful, null if the version check failed.</returns>
    Task<HytaleVersionInfo?> GetServerVersionAsync(
        string patchline = "release",
        HytaleCredentials? credentials = null,
        CancellationToken ct = default);

    /// <summary>
    /// Downloads Hytale server files to the specified destination.
    /// </summary>
    /// <param name="destinationPath">Directory to download server files to.</param>
    /// <param name="patchline">The patchline to download (e.g., "release").</param>
    /// <param name="credentials">Credentials for authenticated download.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<HytaleDownloadResult> DownloadServerAsync(
        string destinationPath,
        string patchline,
        HytaleCredentials credentials,
        IProgress<HytaleDownloadProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Writes credentials to the CLI credentials file for subsequent download operations.
    /// </summary>
    Task WriteCredentialsAsync(HytaleCredentials credentials, CancellationToken ct = default);

    /// <summary>
    /// Reads credentials from the CLI credentials file.
    /// The CLI may update this file during operations (token refresh).
    /// </summary>
    Task<HytaleCredentials?> ReadCredentialsAsync(CancellationToken ct = default);
}

/// <summary>
/// Information about a Hytale server version.
/// </summary>
public record HytaleVersionInfo
{
    /// <summary>
    /// The version string (e.g., "2025.01.31-abc1234").
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// The patchline this version belongs to (e.g., "release", "pre-release").
    /// </summary>
    public required string Patchline { get; init; }

    /// <summary>
    /// When the version was checked.
    /// </summary>
    public DateTime? CheckedAt { get; init; }
}

/// <summary>
/// Result of a Hytale server download operation.
/// </summary>
public record HytaleDownloadResult
{
    /// <summary>
    /// Whether the download succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if the download failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Path where server files were downloaded.
    /// </summary>
    public string? DownloadedFilePath { get; init; }

    /// <summary>
    /// Version of the downloaded server.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Size of downloaded files in bytes.
    /// </summary>
    public long? FileSizeBytes { get; init; }

    /// <summary>
    /// Duration of the download operation.
    /// </summary>
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Progress information for Hytale download operations.
/// </summary>
public record HytaleDownloadProgress
{
    /// <summary>
    /// Current phase of the download operation.
    /// </summary>
    public HytaleDownloadPhase Phase { get; init; }

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    public double Percentage { get; init; }

    /// <summary>
    /// Human-readable progress message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Bytes downloaded so far.
    /// </summary>
    public long? BytesDownloaded { get; init; }

    /// <summary>
    /// Total bytes to download.
    /// </summary>
    public long? TotalBytes { get; init; }

    /// <summary>
    /// Timestamp of this progress update.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public static HytaleDownloadProgress Starting() => new()
    {
        Phase = HytaleDownloadPhase.Starting,
        Message = "Starting download..."
    };

    public static HytaleDownloadProgress Authenticating() => new()
    {
        Phase = HytaleDownloadPhase.Authenticating,
        Message = "Authenticating..."
    };

    public static HytaleDownloadProgress FetchingVersion() => new()
    {
        Phase = HytaleDownloadPhase.FetchingVersion,
        Message = "Fetching version information..."
    };

    public static HytaleDownloadProgress Downloading(double percent, long downloaded, long total) => new()
    {
        Phase = HytaleDownloadPhase.Downloading,
        Percentage = percent,
        Message = $"Downloading... {percent:F1}%",
        BytesDownloaded = downloaded,
        TotalBytes = total
    };

    public static HytaleDownloadProgress Verifying() => new()
    {
        Phase = HytaleDownloadPhase.Verifying,
        Message = "Verifying download..."
    };

    public static HytaleDownloadProgress Extracting() => new()
    {
        Phase = HytaleDownloadPhase.Extracting,
        Message = "Extracting server files..."
    };

    public static HytaleDownloadProgress Complete() => new()
    {
        Phase = HytaleDownloadPhase.Complete,
        Percentage = 100,
        Message = "Download complete"
    };

    public static HytaleDownloadProgress Failed(string error) => new()
    {
        Phase = HytaleDownloadPhase.Complete,
        Message = error
    };
}

/// <summary>
/// Phases of the Hytale download operation.
/// </summary>
public enum HytaleDownloadPhase
{
    Starting,
    Authenticating,
    FetchingVersion,
    Downloading,
    Verifying,
    Extracting,
    Complete
}

/// <summary>
/// OAuth2 device code information for user authentication.
/// </summary>
public record HytaleDeviceCodeInfo
{
    /// <summary>
    /// URL for the user to open (e.g., "https://auth.hytale.com/device").
    /// </summary>
    public required string VerificationUrl { get; init; }

    /// <summary>
    /// Code for the user to enter (e.g., "ABCD-1234").
    /// </summary>
    public required string UserCode { get; init; }

    /// <summary>
    /// When the device code expires.
    /// </summary>
    public DateTime ExpiresAt { get; init; }

    /// <summary>
    /// Recommended polling interval in seconds.
    /// </summary>
    public int PollingIntervalSeconds { get; init; } = 5;
}

/// <summary>
/// Result of a Hytale authentication operation.
/// </summary>
public record HytaleAuthResult
{
    /// <summary>
    /// Whether authentication succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if authentication failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Credentials if authentication succeeded.
    /// </summary>
    public HytaleCredentials? Credentials { get; init; }
}

/// <summary>
/// Hytale OAuth2 credentials (tokens).
/// </summary>
public record HytaleCredentials
{
    /// <summary>
    /// OAuth2 access token.
    /// </summary>
    public required string AccessToken { get; init; }

    /// <summary>
    /// OAuth2 refresh token.
    /// </summary>
    public required string RefreshToken { get; init; }

    /// <summary>
    /// When the access token expires.
    /// </summary>
    public DateTime ExpiresAt { get; init; }

    /// <summary>
    /// Token type (usually "Bearer").
    /// </summary>
    public string TokenType { get; init; } = "Bearer";

    /// <summary>
    /// Whether the credentials are still valid.
    /// </summary>
    public bool IsValid => DateTime.UtcNow < ExpiresAt;

    /// <summary>
    /// Time remaining until the token expires.
    /// </summary>
    public TimeSpan TimeRemaining => ExpiresAt - DateTime.UtcNow;
}

/// <summary>
/// Result of checking for hytale-downloader updates.
/// </summary>
public record HytaleUpdateCheckResult
{
    /// <summary>
    /// Whether an update is available.
    /// </summary>
    public bool UpdateAvailable { get; init; }

    /// <summary>
    /// Current installed version.
    /// </summary>
    public string? CurrentVersion { get; init; }

    /// <summary>
    /// Latest available version.
    /// </summary>
    public string? LatestVersion { get; init; }

    /// <summary>
    /// Error message if the check failed.
    /// </summary>
    public string? Error { get; init; }
}
