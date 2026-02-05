namespace NebulaPanel.Domain.ValueObjects;

/// <summary>
/// Represents the result of downloading an update.
/// </summary>
public record UpdateDownloadResult
{
    /// <summary>
    /// Whether the download was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// The path to the downloaded file, or null if the download failed.
    /// </summary>
    public string? DownloadPath { get; init; }

    /// <summary>
    /// The version that was downloaded.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Error message if the download failed, or null if successful.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Creates a successful download result.
    /// </summary>
    public static UpdateDownloadResult Succeeded(string downloadPath, string version) => new()
    {
        Success = true,
        DownloadPath = downloadPath,
        Version = version
    };

    /// <summary>
    /// Creates a failed download result.
    /// </summary>
    public static UpdateDownloadResult Failed(string error) => new()
    {
        Success = false,
        Error = error
    };
}
