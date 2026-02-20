using System.Diagnostics;
using System.IO.Compression;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Interfaces;

namespace NebulaPanel.Infrastructure.ModProviders;

/// <summary>
/// Extracts mod archives (.zip, .7z) to the correct directory based on archive contents.
/// Detects known path prefixes to determine extraction root automatically.
/// </summary>
public sealed class ModArchiveExtractor(ILogger<ModArchiveExtractor> logger) : IModArchiveExtractor
{
    private readonly ILogger<ModArchiveExtractor> _logger = logger;

    // Known root prefixes that indicate where the archive should be extracted relative to.
    // Order matters — more specific prefixes should come first.
    private static readonly (string Prefix, string RelativeRoot)[] KnownPrefixes =
    [
        ("SPT/", ""),              // Archive contains SPT/ → extract at server root
        ("user/", "SPT"),          // Archive contains user/ → extract inside SPT/
        ("BepInEx/", "SPT"),       // Archive contains BepInEx/ → extract inside SPT/
    ];

    public async Task<ModExtractionResult> ExtractAsync(
        string archivePath,
        string serverInstallPath,
        string modInstallPath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(archivePath))
            return new ModExtractionResult(false, null, $"Archive not found: {archivePath}");

        var extension = Path.GetExtension(archivePath).ToLowerInvariant();

        try
        {
            var entries = extension switch
            {
                ".zip" => GetZipEntries(archivePath),
                ".7z" => await Get7zEntriesAsync(archivePath, cancellationToken).ConfigureAwait(false),
                ".rar" => await Get7zEntriesAsync(archivePath, cancellationToken).ConfigureAwait(false),
                _ => null
            };

            if (entries is null || entries.Count == 0)
            {
                _logger.LogWarning("Could not read entries from archive {Archive} or archive is empty", archivePath);
                return new ModExtractionResult(false, null, $"Unsupported or empty archive: {extension}");
            }

            // Normalize backslashes to forward slashes — Windows-created archives
            // use backslashes which break prefix detection and extraction on Linux.
            entries = entries.Select(e => e.Replace('\\', '/')).ToList();

            _logger.LogInformation("Archive {Archive} contains {Count} entries", archivePath, entries.Count);

            // Determine extraction root based on archive contents
            var extractionRoot = DetermineExtractionRoot(entries, serverInstallPath, modInstallPath);

            _logger.LogInformation("Determined extraction root: {Root}", extractionRoot);

            Directory.CreateDirectory(extractionRoot);

            bool success;
            if (extension == ".zip")
            {
                success = ExtractZip(archivePath, extractionRoot);
            }
            else
            {
                success = await Extract7zAsync(archivePath, extractionRoot, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (!success)
                return new ModExtractionResult(false, null, "Extraction failed");

            // Delete the archive after successful extraction
            try
            {
                File.Delete(archivePath);
                _logger.LogInformation("Deleted archive after extraction: {Archive}", archivePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete archive after extraction: {Archive}", archivePath);
            }

            // Determine the extracted mod path for the database record
            var extractedPath = DetermineExtractedModPath(entries, extractionRoot, serverInstallPath);

            return new ModExtractionResult(true, extractedPath, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract archive {Archive}", archivePath);
            return new ModExtractionResult(false, null, $"Extraction failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Analyzes archive entries to determine where to extract based on known path prefixes.
    /// </summary>
    private string DetermineExtractionRoot(
        IReadOnlyList<string> entries,
        string serverInstallPath,
        string modInstallPath)
    {
        // Check for known path prefixes in the archive entries
        foreach (var (prefix, relativeRoot) in KnownPrefixes)
        {
            if (entries.Any(e => e.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                var root = string.IsNullOrEmpty(relativeRoot)
                    ? serverInstallPath
                    : Path.Combine(serverInstallPath, relativeRoot);

                _logger.LogInformation(
                    "Archive contains '{Prefix}' prefix — extracting to {Root}",
                    prefix, root);
                return root;
            }
        }

        // No known prefix found — extract to the configured mod install path
        var defaultRoot = Path.Combine(serverInstallPath, modInstallPath.TrimEnd('/'));
        _logger.LogInformation(
            "No known prefix found in archive — extracting to default mod path: {Root}",
            defaultRoot);
        return defaultRoot;
    }

    /// <summary>
    /// Determines the relative path from server install root to the extracted mod content.
    /// Used for the LocalPath database field.
    /// IMPORTANT: Returns null when the mod has no single owning directory to prevent
    /// uninstall from deleting shared directories (e.g. the entire mods folder).
    /// </summary>
    private string? DetermineExtractedModPath(
        IReadOnlyList<string> entries,
        string extractionRoot,
        string serverInstallPath)
    {
        // Find the top-level directory in the archive
        var topLevelDirs = entries
            .Select(e => e.Split('/', '\\')[0])
            .Where(d => !string.IsNullOrEmpty(d) && !d.Contains('.')) // Exclude loose files
            .Distinct()
            .ToList();

        if (topLevelDirs.Count == 1)
        {
            // Single top-level directory — that's the mod folder
            var modDir = Path.Combine(extractionRoot, topLevelDirs[0]);
            if (Directory.Exists(modDir))
                return Path.GetRelativePath(serverInstallPath, modDir);
        }

        // Multiple top-level items or loose files — no single mod directory to track.
        // Return null so uninstall won't accidentally delete shared directories.
        _logger.LogWarning(
            "Archive has no single top-level mod directory (found: {Items}). " +
            "LocalPath will be null — files must be cleaned up manually on uninstall.",
            string.Join(", ", entries.Take(10)));
        return null;
    }

    #region ZIP

    private List<string> GetZipEntries(string archivePath)
    {
        using var archive = ZipFile.OpenRead(archivePath);
        return archive.Entries
            .Select(e => e.FullName)
            .Where(e => !string.IsNullOrEmpty(e))
            .ToList();
    }

    private bool ExtractZip(string archivePath, string destinationPath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.FullName))
                    continue;

                // Normalize backslashes — Windows-created zips use \ which
                // becomes a literal character on Linux instead of a path separator
                var normalizedName = entry.FullName.Replace('\\', '/');
                var targetPath = Path.Combine(destinationPath, normalizedName);

                // Prevent path traversal
                var fullTargetPath = Path.GetFullPath(targetPath);
                var fullDestPath = Path.GetFullPath(destinationPath);
                if (!fullTargetPath.StartsWith(fullDestPath, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Skipping entry with path traversal: {Entry}", entry.FullName);
                    continue;
                }

                if (normalizedName.EndsWith('/'))
                {
                    Directory.CreateDirectory(targetPath);
                }
                else
                {
                    var dir = Path.GetDirectoryName(targetPath);
                    if (dir is not null)
                        Directory.CreateDirectory(dir);

                    entry.ExtractToFile(targetPath, overwrite: true);
                }
            }

            _logger.LogInformation("Extracted ZIP archive to {Path}", destinationPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract ZIP archive {Archive}", archivePath);
            return false;
        }
    }

    #endregion

    #region 7z

    private async Task<List<string>?> Get7zEntriesAsync(string archivePath, CancellationToken cancellationToken)
    {
        var sevenZipPath = Find7zBinary();
        if (sevenZipPath is null)
        {
            _logger.LogWarning("7z binary not found — cannot list archive entries");
            return null;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = sevenZipPath,
                Arguments = $"l -slt \"{archivePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null) return null;

            var output = await process.StandardOutput.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogWarning("7z list failed (exit {Code}): {Error}", process.ExitCode, stderr);
                return null;
            }

            // Parse 7z -slt output: entries have "Path = <path>" lines
            var entries = new List<string>();
            foreach (var line in output.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("Path = ", StringComparison.Ordinal) && !trimmed.EndsWith(".7z") && !trimmed.EndsWith(".rar"))
                {
                    var path = trimmed["Path = ".Length..];
                    if (!string.IsNullOrEmpty(path))
                        entries.Add(path);
                }
            }

            return entries;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to list 7z archive entries");
            return null;
        }
    }

    private async Task<bool> Extract7zAsync(string archivePath, string destinationPath, CancellationToken cancellationToken)
    {
        var sevenZipPath = Find7zBinary();
        if (sevenZipPath is null)
        {
            _logger.LogError("7z binary not found — cannot extract archive");
            return false;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = sevenZipPath,
                Arguments = $"x \"{archivePath}\" -o\"{destinationPath}\" -aoa -y",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                _logger.LogError("Failed to start 7z process");
                return false;
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogError("7z extraction failed (exit {Code}): {Error}", process.ExitCode, stderr);
                return false;
            }

            _logger.LogInformation("Extracted 7z archive to {Path}", destinationPath);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract 7z archive {Archive}", archivePath);
            return false;
        }
    }

    private static string? Find7zBinary()
    {
        // Check common locations
        string[] candidates =
        [
            "7z",
            "7za",
            "7zz",
            "/usr/bin/7z",
            "/usr/bin/7za",
            "/usr/bin/7zz",
            "/usr/local/bin/7z",
        ];

        foreach (var candidate in candidates)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = candidate,
                    Arguments = "--help",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process is not null)
                {
                    process.WaitForExit(2000);
                    return candidate;
                }
            }
            catch
            {
                // Not found, try next
            }
        }

        return null;
    }

    #endregion
}
