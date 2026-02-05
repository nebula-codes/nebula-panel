using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Application.DTOs;

/// <summary>
/// Information about an installed Terraria/tModLoader mod.
/// </summary>
public record TerrariaInstalledMod
{
    /// <summary>
    /// Unique identifier for this installation.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Display name of the mod.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// tModLoader internal name (from .tmod file).
    /// </summary>
    public string? InternalName { get; init; }

    /// <summary>
    /// Mod version string.
    /// </summary>
    public string? Version { get; init; }

    /// <summary>
    /// Mod author name.
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// Mod description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// URL to the mod's icon.
    /// </summary>
    public string? IconUrl { get; init; }

    /// <summary>
    /// Steam Workshop item ID if from Workshop.
    /// </summary>
    public string? WorkshopId { get; init; }

    /// <summary>
    /// Whether the mod is currently enabled.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Full path to the installed .tmod file.
    /// </summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// File name of the .tmod file.
    /// </summary>
    public string FileName { get; init; } = string.Empty;

    /// <summary>
    /// Size of the .tmod file in bytes.
    /// </summary>
    public long FileSizeBytes { get; init; }

    /// <summary>
    /// When the mod was installed.
    /// </summary>
    public DateTime? InstalledAt { get; init; }

    /// <summary>
    /// Number of downloads on Workshop.
    /// </summary>
    public long Downloads { get; init; }
}

/// <summary>
/// Result of a single mod installation operation.
/// </summary>
public record TerrariaModInstallResult
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
    public TerrariaInstalledMod? InstalledMod { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static TerrariaModInstallResult Successful(TerrariaInstalledMod mod) => new()
    {
        Success = true,
        InstalledMod = mod
    };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static TerrariaModInstallResult Failed(string error) => new()
    {
        Success = false,
        Error = error
    };
}

/// <summary>
/// Result of a bulk mod installation operation.
/// </summary>
public record TerrarriaModBulkInstallResult
{
    /// <summary>
    /// Whether the overall operation succeeded (at least partially).
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Number of mods successfully installed.
    /// </summary>
    public int InstalledCount { get; init; }

    /// <summary>
    /// Total number of mods attempted.
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// Number of mods skipped (already installed).
    /// </summary>
    public int SkippedCount { get; init; }

    /// <summary>
    /// Number of mods that failed to install.
    /// </summary>
    public int FailedCount { get; init; }

    /// <summary>
    /// Error messages for failed installations.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// General error message if the operation failed entirely.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static TerrarriaModBulkInstallResult Successful(int installed, int total, int skipped = 0) => new()
    {
        Success = true,
        InstalledCount = installed,
        TotalCount = total,
        SkippedCount = skipped,
        FailedCount = total - installed - skipped
    };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static TerrarriaModBulkInstallResult Failed(string error) => new()
    {
        Success = false,
        Error = error
    };
}

/// <summary>
/// Progress information during mod installation.
/// </summary>
public record TerrariaModInstallProgress
{
    /// <summary>
    /// Current phase of the installation.
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
    /// Current mod being processed (for bulk operations).
    /// </summary>
    public int CurrentMod { get; init; }

    /// <summary>
    /// Total mods to process (for bulk operations).
    /// </summary>
    public int TotalMods { get; init; }

    /// <summary>
    /// Creates a downloading progress update.
    /// </summary>
    public static TerrariaModInstallProgress Downloading(string modName, int percentage, int current = 1, int total = 1) => new()
    {
        Phase = "Downloading",
        Message = $"Downloading {modName}...",
        Percentage = percentage,
        CurrentMod = current,
        TotalMods = total
    };

    /// <summary>
    /// Creates an installing progress update.
    /// </summary>
    public static TerrariaModInstallProgress Installing(string modName, int current = 1, int total = 1) => new()
    {
        Phase = "Installing",
        Message = $"Installing {modName}...",
        Percentage = 90,
        CurrentMod = current,
        TotalMods = total
    };

    /// <summary>
    /// Creates a complete progress update.
    /// </summary>
    public static TerrariaModInstallProgress Complete(string modName) => new()
    {
        Phase = "Complete",
        Message = $"{modName} installed successfully",
        Percentage = 100
    };

    /// <summary>
    /// Creates a bulk operation progress update.
    /// </summary>
    public static TerrariaModInstallProgress BulkProgress(string message, int current, int total) => new()
    {
        Phase = "Installing",
        Message = message,
        Percentage = total > 0 ? (current * 100) / total : 0,
        CurrentMod = current,
        TotalMods = total
    };

    /// <summary>
    /// Creates a failed progress update.
    /// </summary>
    public static TerrariaModInstallProgress Failed(string error) => new()
    {
        Phase = "Failed",
        Message = error,
        Percentage = 0
    };
}

/// <summary>
/// Preview of a mod from a modpack or collection.
/// </summary>
public record TerrariaModPreview
{
    /// <summary>
    /// Steam Workshop item ID.
    /// </summary>
    public string WorkshopId { get; init; } = string.Empty;

    /// <summary>
    /// Mod name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Mod author.
    /// </summary>
    public string? Author { get; init; }

    /// <summary>
    /// Mod description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Preview image URL.
    /// </summary>
    public string? IconUrl { get; init; }

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSizeBytes { get; init; }

    /// <summary>
    /// Number of downloads.
    /// </summary>
    public long Downloads { get; init; }

    /// <summary>
    /// Whether this mod is already installed.
    /// </summary>
    public bool AlreadyInstalled { get; init; }
}

/// <summary>
/// Preview of a Steam Workshop collection.
/// </summary>
public record TerrariaCollectionPreview
{
    /// <summary>
    /// Collection ID.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Collection title.
    /// </summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Collection description.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Preview image URL.
    /// </summary>
    public string? PreviewUrl { get; init; }

    /// <summary>
    /// Number of mods in the collection.
    /// </summary>
    public int ModCount { get; init; }

    /// <summary>
    /// List of mods in the collection.
    /// </summary>
    public IReadOnlyList<TerrariaModPreview> Mods { get; init; } = [];

    /// <summary>
    /// Number of mods that are already installed.
    /// </summary>
    public int AlreadyInstalledCount { get; init; }
}

/// <summary>
/// Modpack JSON format for import/export.
/// </summary>
public record TerrariaModpackJson
{
    /// <summary>
    /// Optional modpack name.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// List of mods in the modpack.
    /// </summary>
    public IReadOnlyList<TerrariaModpackEntry> Mods { get; init; } = [];
}

/// <summary>
/// Single mod entry in a modpack.
/// </summary>
public record TerrariaModpackEntry
{
    /// <summary>
    /// Steam Workshop item ID.
    /// </summary>
    public string WorkshopId { get; init; } = string.Empty;

    /// <summary>
    /// Mod name (optional, for display).
    /// </summary>
    public string? Name { get; init; }
}
