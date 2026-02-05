namespace NebulaPanel.Domain.ValueObjects;

/// <summary>
/// Represents the result of checking for updates.
/// </summary>
public record UpdateCheckResult
{
    /// <summary>
    /// Whether an update is available.
    /// </summary>
    public bool UpdateAvailable { get; init; }

    /// <summary>
    /// The latest available version, or null if no update is available.
    /// </summary>
    public VersionInfo? LatestVersion { get; init; }

    /// <summary>
    /// Detailed information about the latest release, or null if no update is available.
    /// </summary>
    public ReleaseInfo? LatestRelease { get; init; }

    /// <summary>
    /// Whether this is a security update.
    /// </summary>
    public bool IsSecurityUpdate { get; init; }

    /// <summary>
    /// Whether this is a major version update.
    /// </summary>
    public bool IsMajorUpdate { get; init; }

    /// <summary>
    /// When the check was performed.
    /// </summary>
    public DateTime CheckedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Error message if the check failed, or null if successful.
    /// </summary>
    public string? Error { get; init; }
}
