using System.Text.Json.Serialization;

namespace NebulaPanel.Infrastructure.ModpackProviders.CurseForge;

/// <summary>
/// CurseForge modpack manifest (manifest.json) root structure.
/// </summary>
public sealed record CurseForgeManifest(
    [property: JsonPropertyName("minecraft")] CurseForgeManifestMinecraft Minecraft,
    [property: JsonPropertyName("manifestType")] string? ManifestType,
    [property: JsonPropertyName("manifestVersion")] int ManifestVersion,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string? Version,
    [property: JsonPropertyName("author")] string? Author,
    [property: JsonPropertyName("files")] CurseForgeManifestFile[] Files,
    [property: JsonPropertyName("overrides")] string? Overrides
);

/// <summary>
/// Minecraft version and mod loader info from manifest.
/// </summary>
public sealed record CurseForgeManifestMinecraft(
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("modLoaders")] CurseForgeManifestModLoader[]? ModLoaders
);

/// <summary>
/// Mod loader specification from manifest.
/// </summary>
public sealed record CurseForgeManifestModLoader(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("primary")] bool Primary
);

/// <summary>
/// A file (mod) entry in the manifest.
/// </summary>
public sealed record CurseForgeManifestFile(
    [property: JsonPropertyName("projectID")] int ProjectId,
    [property: JsonPropertyName("fileID")] int FileId,
    [property: JsonPropertyName("required")] bool Required
);

/// <summary>
/// Result of downloading mods from a manifest.
/// </summary>
public sealed record ManifestDownloadResult
{
    /// <summary>
    /// Whether the overall operation succeeded (some individual mods may have failed).
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// Total number of mods in the manifest.
    /// </summary>
    public int TotalMods { get; init; }

    /// <summary>
    /// Number of mods successfully downloaded.
    /// </summary>
    public int DownloadedMods { get; init; }

    /// <summary>
    /// Number of mods skipped (client-only or optional).
    /// </summary>
    public int SkippedMods { get; init; }

    /// <summary>
    /// List of mods that failed to download.
    /// </summary>
    public List<FailedModDownload> FailedMods { get; init; } = [];

    /// <summary>
    /// Error message if the operation failed entirely.
    /// </summary>
    public string? ErrorMessage { get; init; }

    public static ManifestDownloadResult Succeeded(int total, int downloaded, int skipped, List<FailedModDownload>? failed = null)
        => new()
        {
            Success = true,
            TotalMods = total,
            DownloadedMods = downloaded,
            SkippedMods = skipped,
            FailedMods = failed ?? []
        };

    public static ManifestDownloadResult Failed(string error)
        => new()
        {
            Success = false,
            ErrorMessage = error
        };
}

/// <summary>
/// Information about a mod that failed to download.
/// </summary>
public sealed record FailedModDownload(
    int ProjectId,
    int FileId,
    string? ModName,
    string Reason
);
