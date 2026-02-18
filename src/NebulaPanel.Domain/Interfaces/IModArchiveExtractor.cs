namespace NebulaPanel.Domain.Interfaces;

/// <summary>
/// Extracts mod archives (zip, 7z) to the correct location based on archive structure.
/// </summary>
public interface IModArchiveExtractor
{
    /// <summary>
    /// Extracts a mod archive to the appropriate directory based on its internal structure.
    /// Detects known path prefixes (SPT/, user/, BepInEx/) to determine extraction root.
    /// </summary>
    /// <param name="archivePath">Full path to the archive file.</param>
    /// <param name="serverInstallPath">Root install path of the game server.</param>
    /// <param name="modInstallPath">Relative mod install path (e.g., "SPT/user/mods/").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Extraction result with the path to extracted content.</returns>
    Task<ModExtractionResult> ExtractAsync(
        string archivePath,
        string serverInstallPath,
        string modInstallPath,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a mod archive extraction operation.
/// </summary>
public record ModExtractionResult(
    bool Success,
    string? ExtractedPath,
    string? Error
);
