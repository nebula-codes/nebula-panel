using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;

namespace NebulaPanel.Application.DTOs;

/// <summary>
/// Unified search result aggregating mods from all Hytale providers.
/// </summary>
public record HytaleUnifiedModSearchResult
{
    /// <summary>
    /// Combined mod results from all providers.
    /// </summary>
    public IReadOnlyList<ModSearchItem> Mods { get; init; } = [];

    /// <summary>
    /// Total count of results across all providers.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Current page number (1-indexed).
    /// </summary>
    public int Page { get; init; }

    /// <summary>
    /// Page size used for the search.
    /// </summary>
    public int PageSize { get; init; }

    /// <summary>
    /// Per-provider search information.
    /// </summary>
    public Dictionary<ModProviderType, HytaleProviderSearchInfo> ProviderInfo { get; init; } = new();
}

/// <summary>
/// Information about search results from a specific provider.
/// </summary>
public record HytaleProviderSearchInfo
{
    /// <summary>
    /// Number of results from this provider.
    /// </summary>
    public int ResultCount { get; init; }

    /// <summary>
    /// Whether this provider is available.
    /// </summary>
    public bool Available { get; init; }

    /// <summary>
    /// Optional message about provider status (e.g., "Coming soon").
    /// </summary>
    public string? Message { get; init; }
}

/// <summary>
/// Full information about an installed mod, including update status.
/// </summary>
public record HytaleInstalledModInfo
{
    /// <summary>
    /// Unique identifier for this installation.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Provider-specific mod identifier.
    /// </summary>
    public string ModId { get; init; } = string.Empty;

    /// <summary>
    /// Display name of the mod.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Mod author name.
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// Currently installed version string.
    /// </summary>
    public string InstalledVersion { get; init; } = string.Empty;

    /// <summary>
    /// Provider-specific version identifier.
    /// </summary>
    public string? InstalledVersionId { get; init; }

    /// <summary>
    /// Mod provider type.
    /// </summary>
    public ModProviderType Provider { get; init; }

    /// <summary>
    /// URL to the mod's icon.
    /// </summary>
    public string? IconUrl { get; init; }

    /// <summary>
    /// Full path to the installed file.
    /// </summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// File name of the installed mod.
    /// </summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>
    /// Size of the installed file in bytes.
    /// </summary>
    public long FileSizeBytes { get; init; }

    /// <summary>
    /// When the mod was installed.
    /// </summary>
    public DateTime InstalledAt { get; init; }

    /// <summary>
    /// Whether the mod is currently enabled.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Whether an update is available.
    /// </summary>
    public bool UpdateAvailable { get; init; }

    /// <summary>
    /// Latest available version string.
    /// </summary>
    public string? LatestVersion { get; init; }

    /// <summary>
    /// Provider-specific identifier for the latest version.
    /// </summary>
    public string? LatestVersionId { get; init; }

    /// <summary>
    /// Release date of the latest version.
    /// </summary>
    public DateTime? LatestVersionDate { get; init; }
}

/// <summary>
/// Information about an available update for an installed mod.
/// </summary>
public record HytaleModUpdateInfo
{
    /// <summary>
    /// ID of the installed mod record.
    /// </summary>
    public Guid InstalledModId { get; init; }

    /// <summary>
    /// Display name of the mod.
    /// </summary>
    public string ModName { get; init; } = string.Empty;

    /// <summary>
    /// Currently installed version string.
    /// </summary>
    public string CurrentVersion { get; init; } = string.Empty;

    /// <summary>
    /// Latest available version string.
    /// </summary>
    public string LatestVersion { get; init; } = string.Empty;

    /// <summary>
    /// Provider-specific identifier for the latest version.
    /// </summary>
    public string LatestVersionId { get; init; } = string.Empty;

    /// <summary>
    /// Changelog for the latest version.
    /// </summary>
    public string? Changelog { get; init; }

    /// <summary>
    /// Release date of the latest version.
    /// </summary>
    public DateTime? ReleasedAt { get; init; }
}

/// <summary>
/// Result of a mod installation operation.
/// </summary>
public record HytaleModInstallResult
{
    /// <summary>
    /// Whether the installation succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if installation failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Information about the installed mod if successful.
    /// </summary>
    public HytaleInstalledModInfo? InstalledMod { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static HytaleModInstallResult Successful(HytaleInstalledModInfo mod) => new()
    {
        Success = true,
        InstalledMod = mod
    };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static HytaleModInstallResult Failed(string error) => new()
    {
        Success = false,
        Error = error
    };
}

/// <summary>
/// Progress information during mod installation.
/// </summary>
public record HytaleModInstallProgress
{
    /// <summary>
    /// Current phase of the installation (e.g., "Downloading", "Registering").
    /// </summary>
    public string Phase { get; init; } = string.Empty;

    /// <summary>
    /// Human-readable progress message.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    public int Percentage { get; init; }

    /// <summary>
    /// Creates a downloading progress update.
    /// </summary>
    public static HytaleModInstallProgress Downloading(string modName, int percentage) => new()
    {
        Phase = "Downloading",
        Message = $"Downloading {modName}...",
        Percentage = percentage
    };

    /// <summary>
    /// Creates a registering progress update.
    /// </summary>
    public static HytaleModInstallProgress Registering() => new()
    {
        Phase = "Registering",
        Message = "Registering mod...",
        Percentage = 95
    };

    /// <summary>
    /// Creates a complete progress update.
    /// </summary>
    public static HytaleModInstallProgress Complete(string modName) => new()
    {
        Phase = "Complete",
        Message = $"{modName} installed successfully",
        Percentage = 100
    };

    /// <summary>
    /// Creates a failed progress update.
    /// </summary>
    public static HytaleModInstallProgress Failed(string error) => new()
    {
        Phase = "Failed",
        Message = error,
        Percentage = 0
    };
}
