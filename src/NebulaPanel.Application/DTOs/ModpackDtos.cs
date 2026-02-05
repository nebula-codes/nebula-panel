using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;

namespace NebulaPanel.Application.DTOs;

/// <summary>
/// Aggregated search results from multiple modpack providers.
/// </summary>
public record UnifiedModpackSearchResult
{
    /// <summary>
    /// Combined list of modpacks from all searched providers.
    /// </summary>
    public IReadOnlyList<ModpackSearchItem> Modpacks { get; init; } = [];

    /// <summary>
    /// Count of results from each provider.
    /// </summary>
    public Dictionary<ModpackProviderType, int> ResultCountByProvider { get; init; } = [];

    /// <summary>
    /// Total number of results found across all providers.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Whether there are more results available beyond the current page.
    /// </summary>
    public bool HasMoreResults { get; init; }
}

/// <summary>
/// Information about a modpack provider.
/// </summary>
public record ModpackProviderInfo
{
    /// <summary>
    /// The provider type identifier.
    /// </summary>
    public ModpackProviderType Type { get; init; }

    /// <summary>
    /// Display name of the provider.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// URL to the provider's icon/logo.
    /// </summary>
    public string? IconUrl { get; init; }

    /// <summary>
    /// Whether the provider is enabled for use.
    /// </summary>
    public bool Enabled { get; init; }
}

/// <summary>
/// Result of a modpack installation attempt.
/// </summary>
public record ModpackInstallResult
{
    /// <summary>
    /// Whether the installation was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if installation failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Path where the modpack was installed.
    /// </summary>
    public string? InstallPath { get; init; }

    /// <summary>
    /// Name of the installed modpack.
    /// </summary>
    public string? ModpackName { get; init; }

    /// <summary>
    /// Version of the installed modpack.
    /// </summary>
    public string? ModpackVersion { get; init; }

    /// <summary>
    /// Minecraft version targeted by the modpack.
    /// </summary>
    public string? MinecraftVersion { get; init; }

    /// <summary>
    /// Mod loader type used by the modpack.
    /// </summary>
    public ModLoaderType? LoaderType { get; init; }

    /// <summary>
    /// Specific loader version used.
    /// </summary>
    public string? LoaderVersion { get; init; }

    /// <summary>
    /// Number of mods included in the modpack.
    /// </summary>
    public int ModCount { get; init; }

    /// <summary>
    /// Creates a successful installation result.
    /// </summary>
    public static ModpackInstallResult Succeeded(
        string installPath,
        string modpackName,
        string modpackVersion,
        string minecraftVersion,
        ModLoaderType loaderType,
        string? loaderVersion = null,
        int modCount = 0)
        => new()
        {
            Success = true,
            InstallPath = installPath,
            ModpackName = modpackName,
            ModpackVersion = modpackVersion,
            MinecraftVersion = minecraftVersion,
            LoaderType = loaderType,
            LoaderVersion = loaderVersion,
            ModCount = modCount
        };

    /// <summary>
    /// Creates a failed installation result.
    /// </summary>
    public static ModpackInstallResult Failed(string error)
        => new()
        {
            Success = false,
            Error = error
        };
}

/// <summary>
/// Progress information during modpack installation.
/// </summary>
public record ModpackInstallProgress
{
    /// <summary>
    /// Current phase of the installation process.
    /// </summary>
    public ModpackInstallPhase Phase { get; init; }

    /// <summary>
    /// Human-readable status message.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// Overall progress percentage (0-100).
    /// </summary>
    public double Percentage { get; init; }

    /// <summary>
    /// Bytes downloaded so far.
    /// </summary>
    public long BytesDownloaded { get; init; }

    /// <summary>
    /// Total bytes to download.
    /// </summary>
    public long TotalBytes { get; init; }

    /// <summary>
    /// Current file being processed.
    /// </summary>
    public string? CurrentFile { get; init; }

    /// <summary>
    /// Current step in the installation (e.g., "1/5").
    /// </summary>
    public string? CurrentStep { get; init; }

    /// <summary>
    /// Creates an initializing progress update.
    /// </summary>
    public static ModpackInstallProgress Initializing()
        => new()
        {
            Phase = ModpackInstallPhase.Initializing,
            Message = "Preparing installation...",
            Percentage = 0
        };

    /// <summary>
    /// Creates a downloading progress update.
    /// </summary>
    public static ModpackInstallProgress Downloading(long bytesDownloaded, long totalBytes, string? currentFile = null)
        => new()
        {
            Phase = ModpackInstallPhase.Downloading,
            Message = "Downloading modpack files...",
            Percentage = totalBytes > 0 ? (double)bytesDownloaded / totalBytes * 50 : 0,
            BytesDownloaded = bytesDownloaded,
            TotalBytes = totalBytes,
            CurrentFile = currentFile
        };

    /// <summary>
    /// Creates an extracting progress update.
    /// </summary>
    public static ModpackInstallProgress Extracting(double percentage, string? currentFile = null)
        => new()
        {
            Phase = ModpackInstallPhase.Extracting,
            Message = "Extracting files...",
            Percentage = 50 + (percentage * 0.2),
            CurrentFile = currentFile
        };

    /// <summary>
    /// Creates an installing loader progress update.
    /// </summary>
    public static ModpackInstallProgress InstallingLoader(string loaderName)
        => new()
        {
            Phase = ModpackInstallPhase.InstallingLoader,
            Message = $"Installing {loaderName}...",
            Percentage = 75
        };

    /// <summary>
    /// Creates a configuring progress update.
    /// </summary>
    public static ModpackInstallProgress Configuring()
        => new()
        {
            Phase = ModpackInstallPhase.Configuring,
            Message = "Configuring server...",
            Percentage = 90
        };

    /// <summary>
    /// Creates a completed progress update.
    /// </summary>
    public static ModpackInstallProgress Completed()
        => new()
        {
            Phase = ModpackInstallPhase.Completed,
            Message = "Installation complete",
            Percentage = 100
        };
}

/// <summary>
/// Phase of the modpack installation process.
/// </summary>
public enum ModpackInstallPhase
{
    /// <summary>
    /// Preparing for installation.
    /// </summary>
    Initializing,

    /// <summary>
    /// Downloading modpack files.
    /// </summary>
    Downloading,

    /// <summary>
    /// Extracting archive contents.
    /// </summary>
    Extracting,

    /// <summary>
    /// Installing the mod loader (Forge/Fabric/etc).
    /// </summary>
    InstallingLoader,

    /// <summary>
    /// Configuring server settings.
    /// </summary>
    Configuring,

    /// <summary>
    /// Installation completed.
    /// </summary>
    Completed
}

/// <summary>
/// Request parameters for installing a modpack on a game server.
/// </summary>
public record ModpackInstallRequest
{
    /// <summary>
    /// The provider the modpack is being installed from.
    /// </summary>
    public ModpackProviderType Provider { get; init; }

    /// <summary>
    /// Provider-specific modpack identifier.
    /// </summary>
    public required string ModpackId { get; init; }

    /// <summary>
    /// Provider-specific version identifier.
    /// </summary>
    public required string VersionId { get; init; }

    /// <summary>
    /// Display name of the modpack.
    /// </summary>
    public required string ModpackName { get; init; }

    /// <summary>
    /// Human-readable version name.
    /// </summary>
    public string? VersionName { get; init; }

    /// <summary>
    /// Target Minecraft version.
    /// </summary>
    public required string MinecraftVersion { get; init; }

    /// <summary>
    /// Mod loader type required by the modpack.
    /// </summary>
    public ModLoaderType Loader { get; init; }

    /// <summary>
    /// Specific loader version to install.
    /// </summary>
    public string? LoaderVersion { get; init; }

    /// <summary>
    /// Recommended RAM in megabytes.
    /// </summary>
    public long? RecommendedRamMb { get; init; }

    /// <summary>
    /// Number of mods in the modpack.
    /// </summary>
    public int? ModCount { get; init; }
}
