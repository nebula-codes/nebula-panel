using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;

namespace NebulaPanel.Application.DTOs;

/// <summary>
/// Information about an available modpack update.
/// </summary>
public record ModpackUpdateInfo
{
    /// <summary>
    /// Currently installed version ID.
    /// </summary>
    public required string CurrentVersionId { get; init; }

    /// <summary>
    /// Currently installed version name.
    /// </summary>
    public required string CurrentVersionName { get; init; }

    /// <summary>
    /// Latest available version ID.
    /// </summary>
    public required string LatestVersionId { get; init; }

    /// <summary>
    /// Latest available version name.
    /// </summary>
    public required string LatestVersionName { get; init; }

    /// <summary>
    /// When the latest version was released.
    /// </summary>
    public required DateTime ReleasedAt { get; init; }

    /// <summary>
    /// Changelog for the latest version (may be HTML or markdown).
    /// </summary>
    public string? Changelog { get; init; }

    /// <summary>
    /// Whether this update is considered a breaking change.
    /// </summary>
    public bool IsBreakingChange { get; init; }

    /// <summary>
    /// Whether the Minecraft version changes with this update.
    /// </summary>
    public bool IsMinecraftVersionChange { get; init; }

    /// <summary>
    /// Whether the mod loader type changes with this update.
    /// </summary>
    public bool IsLoaderChange { get; init; }

    /// <summary>
    /// Current Minecraft version.
    /// </summary>
    public string? CurrentMinecraftVersion { get; init; }

    /// <summary>
    /// New Minecraft version after update.
    /// </summary>
    public string? NewMinecraftVersion { get; init; }

    /// <summary>
    /// Current mod loader type.
    /// </summary>
    public ModLoaderType? CurrentLoaderType { get; init; }

    /// <summary>
    /// New mod loader type after update.
    /// </summary>
    public ModLoaderType? NewLoaderType { get; init; }

    /// <summary>
    /// New mod loader version after update.
    /// </summary>
    public string? NewLoaderVersion { get; init; }

    /// <summary>
    /// Download size of the server pack in bytes.
    /// </summary>
    public long? ServerPackSizeBytes { get; init; }

    /// <summary>
    /// Warnings to display to the user about this update.
    /// </summary>
    public List<string> Warnings { get; init; } = [];

    /// <summary>
    /// List of versions between current and latest.
    /// </summary>
    public List<ModpackVersionSummary> IntermediateVersions { get; init; } = [];
}

/// <summary>
/// Summary of a modpack version for display in version lists.
/// </summary>
public record ModpackVersionSummary
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required DateTime ReleasedAt { get; init; }
    public ModpackVersionType Type { get; init; }
    public string? MinecraftVersion { get; init; }
    public ModLoaderType LoaderType { get; init; }
    public bool HasServerPack { get; init; }
    public long? ServerPackSizeBytes { get; init; }
}

/// <summary>
/// Options for a modpack update operation.
/// </summary>
public record ModpackUpdateOptions
{
    /// <summary>
    /// Whether to create a backup before updating.
    /// </summary>
    public bool CreateBackupBeforeUpdate { get; init; } = true;

    /// <summary>
    /// Whether to preserve world data (world folders).
    /// </summary>
    public bool PreserveWorldData { get; init; } = true;

    /// <summary>
    /// Whether to preserve custom configurations.
    /// </summary>
    public bool PreserveCustomConfigs { get; init; } = true;

    /// <summary>
    /// Whether to preserve player data (whitelist, ops, bans).
    /// </summary>
    public bool PreservePlayerData { get; init; } = true;

    /// <summary>
    /// Whether to restart the server after a successful update.
    /// </summary>
    public bool RestartServerAfterUpdate { get; init; } = true;
}

/// <summary>
/// Result of a modpack update operation.
/// </summary>
public record ModpackUpdateResult
{
    /// <summary>
    /// Whether the update was successful.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Error message if the update failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// ID of the backup created before updating (if any).
    /// </summary>
    public Guid? BackupId { get; init; }

    /// <summary>
    /// The version ID before the update.
    /// </summary>
    public string? PreviousVersionId { get; init; }

    /// <summary>
    /// The version ID after the update.
    /// </summary>
    public string? NewVersionId { get; init; }

    /// <summary>
    /// List of paths that were preserved during the update.
    /// </summary>
    public List<string> PreservedPaths { get; init; } = [];

    /// <summary>
    /// List of config conflicts that occurred during the update.
    /// </summary>
    public List<ConfigConflict> ConfigConflicts { get; init; } = [];

    /// <summary>
    /// Creates a successful update result.
    /// </summary>
    public static ModpackUpdateResult Succeeded(
        Guid? backupId,
        string previousVersionId,
        string newVersionId,
        List<string>? preservedPaths = null,
        List<ConfigConflict>? configConflicts = null) => new()
    {
        Success = true,
        BackupId = backupId,
        PreviousVersionId = previousVersionId,
        NewVersionId = newVersionId,
        PreservedPaths = preservedPaths ?? [],
        ConfigConflicts = configConflicts ?? []
    };

    /// <summary>
    /// Creates a failed update result.
    /// </summary>
    public static ModpackUpdateResult Failed(string error) => new()
    {
        Success = false,
        Error = error
    };
}

/// <summary>
/// Information about a config file conflict during update.
/// </summary>
public record ConfigConflict
{
    /// <summary>
    /// Relative path of the config file.
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// How the conflict was resolved.
    /// </summary>
    public required ConfigConflictResolution Resolution { get; init; }

    /// <summary>
    /// Additional details about the conflict resolution.
    /// </summary>
    public string? Details { get; init; }
}

/// <summary>
/// How a config conflict was resolved.
/// </summary>
public enum ConfigConflictResolution
{
    /// <summary>
    /// Kept the user's modified version.
    /// </summary>
    KeptUserVersion,

    /// <summary>
    /// Used the new version from the modpack.
    /// </summary>
    UsedNewVersion,

    /// <summary>
    /// Merged changes from both versions.
    /// </summary>
    Merged,

    /// <summary>
    /// Backed up both versions for manual resolution.
    /// </summary>
    BackedUp
}

/// <summary>
/// Progress information for a modpack update operation.
/// </summary>
public record ModpackUpdateProgressDto
{
    /// <summary>
    /// Current phase of the update.
    /// </summary>
    public required ModpackUpdatePhase Phase { get; init; }

    /// <summary>
    /// Human-readable message about current progress.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Overall progress percentage (0-100).
    /// </summary>
    public double Percentage { get; init; }

    /// <summary>
    /// Bytes downloaded so far (for download phase).
    /// </summary>
    public long? BytesDownloaded { get; init; }

    /// <summary>
    /// Total bytes to download (for download phase).
    /// </summary>
    public long? TotalBytes { get; init; }

    /// <summary>
    /// Current file being processed.
    /// </summary>
    public string? CurrentFile { get; init; }

    /// <summary>
    /// Timestamp of this progress update.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    // Factory methods for common progress states

    public static ModpackUpdateProgressDto Initializing() => new()
    {
        Phase = ModpackUpdatePhase.Initializing,
        Message = "Preparing update...",
        Percentage = 0
    };

    public static ModpackUpdateProgressDto StoppingServer() => new()
    {
        Phase = ModpackUpdatePhase.StoppingServer,
        Message = "Stopping server...",
        Percentage = 5
    };

    public static ModpackUpdateProgressDto CreatingBackup() => new()
    {
        Phase = ModpackUpdatePhase.CreatingBackup,
        Message = "Creating backup...",
        Percentage = 10
    };

    public static ModpackUpdateProgressDto Downloading(long bytesDownloaded, long totalBytes) => new()
    {
        Phase = ModpackUpdatePhase.Downloading,
        Message = "Downloading new version...",
        Percentage = 15 + (bytesDownloaded / (double)totalBytes * 35),
        BytesDownloaded = bytesDownloaded,
        TotalBytes = totalBytes
    };

    public static ModpackUpdateProgressDto Extracting(double extractPercent) => new()
    {
        Phase = ModpackUpdatePhase.Extracting,
        Message = "Extracting files...",
        Percentage = 50 + (extractPercent * 0.15)
    };

    public static ModpackUpdateProgressDto PreservingFiles() => new()
    {
        Phase = ModpackUpdatePhase.PreservingFiles,
        Message = "Preserving user data...",
        Percentage = 70
    };

    public static ModpackUpdateProgressDto InstallingLoader() => new()
    {
        Phase = ModpackUpdatePhase.InstallingLoader,
        Message = "Installing mod loader...",
        Percentage = 80
    };

    public static ModpackUpdateProgressDto Configuring() => new()
    {
        Phase = ModpackUpdatePhase.Configuring,
        Message = "Configuring server...",
        Percentage = 90
    };

    public static ModpackUpdateProgressDto StartingServer() => new()
    {
        Phase = ModpackUpdatePhase.StartingServer,
        Message = "Starting server...",
        Percentage = 95
    };

    public static ModpackUpdateProgressDto Completed() => new()
    {
        Phase = ModpackUpdatePhase.Completed,
        Message = "Update complete!",
        Percentage = 100
    };

    public static ModpackUpdateProgressDto Failed(string error) => new()
    {
        Phase = ModpackUpdatePhase.Failed,
        Message = error,
        Percentage = 0
    };
}

/// <summary>
/// Phases of a modpack update operation.
/// </summary>
public enum ModpackUpdatePhase
{
    Initializing,
    StoppingServer,
    CreatingBackup,
    Downloading,
    Extracting,
    PreservingFiles,
    InstallingLoader,
    Configuring,
    StartingServer,
    Completed,
    Failed
}

/// <summary>
/// Status of modpack update availability for a server.
/// </summary>
public record ModpackUpdateStatusDto
{
    /// <summary>
    /// Whether a modpack is installed on this server.
    /// </summary>
    public bool HasModpack { get; init; }

    /// <summary>
    /// Whether an update is available.
    /// </summary>
    public bool UpdateAvailable { get; init; }

    /// <summary>
    /// Current installed version name.
    /// </summary>
    public string? CurrentVersion { get; init; }

    /// <summary>
    /// Latest available version name.
    /// </summary>
    public string? LatestVersion { get; init; }

    /// <summary>
    /// When the update check was last performed.
    /// </summary>
    public DateTime? LastCheckedAt { get; init; }
}
