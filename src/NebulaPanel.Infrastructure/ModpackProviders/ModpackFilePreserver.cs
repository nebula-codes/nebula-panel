using Microsoft.Extensions.Logging;
using NebulaPanel.Application.DTOs;

namespace NebulaPanel.Infrastructure.ModpackProviders;

/// <summary>
/// Handles file preservation during modpack updates.
/// Ensures user data like worlds, configs, and player data are preserved.
/// </summary>
public class ModpackFilePreserver(ILogger<ModpackFilePreserver> logger)
{
    private readonly ILogger<ModpackFilePreserver> _logger = logger;

    /// <summary>
    /// Files/directories that should always be preserved during updates.
    /// </summary>
    private static readonly string[] AlwaysPreserve =
    [
        "world",
        "worlds",
        "world_nether",
        "world_the_end",
        "DIM-1",      // Nether dimension (sometimes separate)
        "DIM1",       // End dimension (sometimes separate)
    ];

    /// <summary>
    /// Player data files that should always be preserved.
    /// </summary>
    private static readonly string[] PlayerDataFiles =
    [
        "whitelist.json",
        "ops.json",
        "banned-ips.json",
        "banned-players.json",
        "usercache.json",
        "permissions.yml",   // Bukkit/Spigot
        "luckperms",         // LuckPerms directory
    ];

    /// <summary>
    /// Config directories that may contain user modifications.
    /// </summary>
    private static readonly string[] ConfigDirectories =
    [
        "config",
        "serverconfig",
        "defaultconfigs",
    ];

    /// <summary>
    /// Files/directories that should never be preserved (always replaced).
    /// </summary>
    private static readonly string[] NeverPreserve =
    [
        "mods",
        "libraries",
        ".fabric",
        "versions",
    ];

    /// <summary>
    /// Saves user files to a temporary location before modpack update.
    /// </summary>
    /// <param name="serverPath">Path to the server installation.</param>
    /// <param name="options">Update options specifying what to preserve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Path to the temporary directory containing preserved files.</returns>
    public async Task<PreservationResult> SaveUserFilesAsync(
        string serverPath,
        ModpackUpdateOptions options,
        CancellationToken ct = default)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"nebula-modpack-preserve-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempPath);

        var preservedPaths = new List<string>();

        _logger.LogInformation("Preserving user files from {ServerPath} to {TempPath}", serverPath, tempPath);

        try
        {
            // Preserve world data
            if (options.PreserveWorldData)
            {
                foreach (var worldDir in AlwaysPreserve)
                {
                    var sourcePath = Path.Combine(serverPath, worldDir);
                    if (Directory.Exists(sourcePath))
                    {
                        var destPath = Path.Combine(tempPath, worldDir);
                        await CopyDirectoryAsync(sourcePath, destPath, ct).ConfigureAwait(false);
                        preservedPaths.Add(worldDir);
                        _logger.LogDebug("Preserved world directory: {Directory}", worldDir);
                    }
                }
            }

            // Preserve player data
            if (options.PreservePlayerData)
            {
                foreach (var playerFile in PlayerDataFiles)
                {
                    var sourcePath = Path.Combine(serverPath, playerFile);
                    var destPath = Path.Combine(tempPath, playerFile);

                    if (File.Exists(sourcePath))
                    {
                        File.Copy(sourcePath, destPath, overwrite: true);
                        preservedPaths.Add(playerFile);
                        _logger.LogDebug("Preserved player data file: {File}", playerFile);
                    }
                    else if (Directory.Exists(sourcePath))
                    {
                        await CopyDirectoryAsync(sourcePath, destPath, ct).ConfigureAwait(false);
                        preservedPaths.Add(playerFile);
                        _logger.LogDebug("Preserved player data directory: {Directory}", playerFile);
                    }
                }
            }

            // Preserve custom configs
            if (options.PreserveCustomConfigs)
            {
                // Save server.properties for later merging
                var serverPropsPath = Path.Combine(serverPath, "server.properties");
                if (File.Exists(serverPropsPath))
                {
                    File.Copy(serverPropsPath, Path.Combine(tempPath, "server.properties.user"), overwrite: true);
                    preservedPaths.Add("server.properties");
                    _logger.LogDebug("Preserved server.properties for merging");
                }

                // Preserve config directories
                foreach (var configDir in ConfigDirectories)
                {
                    var sourcePath = Path.Combine(serverPath, configDir);
                    if (Directory.Exists(sourcePath))
                    {
                        var destPath = Path.Combine(tempPath, configDir);
                        await CopyDirectoryAsync(sourcePath, destPath, ct).ConfigureAwait(false);
                        preservedPaths.Add(configDir);
                        _logger.LogDebug("Preserved config directory: {Directory}", configDir);
                    }
                }
            }

            _logger.LogInformation("Preserved {Count} paths to temporary location", preservedPaths.Count);

            return new PreservationResult
            {
                Success = true,
                TempPath = tempPath,
                PreservedPaths = preservedPaths
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to preserve user files");

            // Clean up temp directory on failure
            try
            {
                if (Directory.Exists(tempPath))
                {
                    Directory.Delete(tempPath, recursive: true);
                }
            }
            catch { /* Ignore cleanup errors */ }

            return new PreservationResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Restores preserved user files after modpack update.
    /// </summary>
    /// <param name="serverPath">Path to the server installation.</param>
    /// <param name="tempPath">Path to the temporary directory with preserved files.</param>
    /// <param name="options">Update options specifying what was preserved.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of config conflicts encountered during restoration.</returns>
    public async Task<RestorationResult> RestoreUserFilesAsync(
        string serverPath,
        string tempPath,
        ModpackUpdateOptions options,
        CancellationToken ct = default)
    {
        var conflicts = new List<ConfigConflict>();
        var restoredPaths = new List<string>();

        _logger.LogInformation("Restoring user files from {TempPath} to {ServerPath}", tempPath, serverPath);

        try
        {
            // Restore world data
            if (options.PreserveWorldData)
            {
                foreach (var worldDir in AlwaysPreserve)
                {
                    var sourcePath = Path.Combine(tempPath, worldDir);
                    if (Directory.Exists(sourcePath))
                    {
                        var destPath = Path.Combine(serverPath, worldDir);

                        // Remove new world if it exists (we want to keep user's world)
                        if (Directory.Exists(destPath))
                        {
                            Directory.Delete(destPath, recursive: true);
                        }

                        await CopyDirectoryAsync(sourcePath, destPath, ct).ConfigureAwait(false);
                        restoredPaths.Add(worldDir);
                        _logger.LogDebug("Restored world directory: {Directory}", worldDir);
                    }
                }
            }

            // Restore player data
            if (options.PreservePlayerData)
            {
                foreach (var playerFile in PlayerDataFiles)
                {
                    var sourcePath = Path.Combine(tempPath, playerFile);
                    var destPath = Path.Combine(serverPath, playerFile);

                    if (File.Exists(sourcePath))
                    {
                        File.Copy(sourcePath, destPath, overwrite: true);
                        restoredPaths.Add(playerFile);
                        _logger.LogDebug("Restored player data file: {File}", playerFile);
                    }
                    else if (Directory.Exists(sourcePath))
                    {
                        if (Directory.Exists(destPath))
                        {
                            Directory.Delete(destPath, recursive: true);
                        }
                        await CopyDirectoryAsync(sourcePath, destPath, ct).ConfigureAwait(false);
                        restoredPaths.Add(playerFile);
                        _logger.LogDebug("Restored player data directory: {Directory}", playerFile);
                    }
                }
            }

            // Merge server.properties
            if (options.PreserveCustomConfigs)
            {
                var userPropsPath = Path.Combine(tempPath, "server.properties.user");
                var newPropsPath = Path.Combine(serverPath, "server.properties");

                if (File.Exists(userPropsPath) && File.Exists(newPropsPath))
                {
                    var mergeResult = await MergeServerPropertiesAsync(
                        userPropsPath, newPropsPath, ct).ConfigureAwait(false);

                    if (mergeResult.HasConflicts)
                    {
                        conflicts.Add(new ConfigConflict
                        {
                            FilePath = "server.properties",
                            Resolution = ConfigConflictResolution.Merged,
                            Details = $"Preserved {mergeResult.PreservedCount} user customizations"
                        });
                    }

                    restoredPaths.Add("server.properties");
                }

                // Restore config directories (merge strategy: keep user files)
                foreach (var configDir in ConfigDirectories)
                {
                    var sourcePath = Path.Combine(tempPath, configDir);
                    var destPath = Path.Combine(serverPath, configDir);

                    if (Directory.Exists(sourcePath))
                    {
                        var configConflicts = await MergeConfigDirectoryAsync(
                            sourcePath, destPath, ct).ConfigureAwait(false);

                        conflicts.AddRange(configConflicts);
                        restoredPaths.Add(configDir);
                        _logger.LogDebug("Merged config directory: {Directory}", configDir);
                    }
                }
            }

            _logger.LogInformation("Restored {Count} paths, {ConflictCount} conflicts",
                restoredPaths.Count, conflicts.Count);

            return new RestorationResult
            {
                Success = true,
                RestoredPaths = restoredPaths,
                Conflicts = conflicts
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore user files");
            return new RestorationResult
            {
                Success = false,
                Error = ex.Message,
                Conflicts = conflicts
            };
        }
    }

    /// <summary>
    /// Cleans up the temporary preservation directory.
    /// </summary>
    public Task CleanupTempFilesAsync(string tempPath, CancellationToken ct = default)
    {
        try
        {
            if (Directory.Exists(tempPath))
            {
                Directory.Delete(tempPath, recursive: true);
                _logger.LogDebug("Cleaned up temporary preservation directory: {Path}", tempPath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up temporary directory: {Path}", tempPath);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Merges user customizations from old server.properties into new server.properties.
    /// </summary>
    private async Task<ServerPropertiesMergeResult> MergeServerPropertiesAsync(
        string userPropsPath,
        string newPropsPath,
        CancellationToken ct)
    {
        var userProps = await ParsePropertiesFileAsync(userPropsPath, ct).ConfigureAwait(false);
        var newProps = await ParsePropertiesFileAsync(newPropsPath, ct).ConfigureAwait(false);

        // Properties that users commonly customize
        var userCustomizableProps = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "server-port",
            "max-players",
            "motd",
            "difficulty",
            "gamemode",
            "view-distance",
            "simulation-distance",
            "spawn-protection",
            "pvp",
            "allow-flight",
            "white-list",
            "enforce-whitelist",
            "enable-command-block",
            "max-world-size",
            "rcon.port",
            "rcon.password",
            "enable-rcon",
            "query.port",
            "enable-query",
            "level-name",
            "level-seed",
            "level-type",
            "generator-settings",
            "online-mode",
        };

        var preservedCount = 0;
        var mergedProps = new Dictionary<string, string>(newProps);

        foreach (var kvp in userProps)
        {
            // Keep user's value for customizable properties
            if (userCustomizableProps.Contains(kvp.Key))
            {
                mergedProps[kvp.Key] = kvp.Value;
                preservedCount++;
            }
        }

        // Write merged properties back
        await WritePropertiesFileAsync(newPropsPath, mergedProps, ct).ConfigureAwait(false);

        return new ServerPropertiesMergeResult
        {
            HasConflicts = preservedCount > 0,
            PreservedCount = preservedCount
        };
    }

    /// <summary>
    /// Merges a config directory, keeping user-modified files.
    /// </summary>
    private async Task<List<ConfigConflict>> MergeConfigDirectoryAsync(
        string userConfigPath,
        string newConfigPath,
        CancellationToken ct)
    {
        var conflicts = new List<ConfigConflict>();

        if (!Directory.Exists(newConfigPath))
        {
            // New modpack doesn't have this config dir, just copy user's
            await CopyDirectoryAsync(userConfigPath, newConfigPath, ct).ConfigureAwait(false);
            return conflicts;
        }

        // For each file in user's config directory
        foreach (var userFile in Directory.GetFiles(userConfigPath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(userConfigPath, userFile);
            var newFilePath = Path.Combine(newConfigPath, relativePath);

            if (File.Exists(newFilePath))
            {
                // Both have the file - compare and keep user version if different
                var userContent = await File.ReadAllBytesAsync(userFile, ct).ConfigureAwait(false);
                var newContent = await File.ReadAllBytesAsync(newFilePath, ct).ConfigureAwait(false);

                if (!userContent.SequenceEqual(newContent))
                {
                    // User modified this file - keep user version
                    File.Copy(userFile, newFilePath, overwrite: true);
                    conflicts.Add(new ConfigConflict
                    {
                        FilePath = relativePath,
                        Resolution = ConfigConflictResolution.KeptUserVersion,
                        Details = "User modified file preserved"
                    });
                }
            }
            else
            {
                // New modpack doesn't have this file - keep user's file
                var destDir = Path.GetDirectoryName(newFilePath);
                if (!string.IsNullOrEmpty(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }
                File.Copy(userFile, newFilePath);
            }
        }

        return conflicts;
    }

    private static async Task<Dictionary<string, string>> ParsePropertiesFileAsync(
        string path, CancellationToken ct)
    {
        var props = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var lines = await File.ReadAllLinesAsync(path, ct).ConfigureAwait(false);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                continue;

            var eqIndex = trimmed.IndexOf('=');
            if (eqIndex > 0)
            {
                var key = trimmed[..eqIndex].Trim();
                var value = trimmed[(eqIndex + 1)..].Trim();
                props[key] = value;
            }
        }

        return props;
    }

    private static async Task WritePropertiesFileAsync(
        string path, Dictionary<string, string> props, CancellationToken ct)
    {
        var lines = new List<string>
        {
            "#Minecraft server properties",
            $"#Generated by Nebula Panel on {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC",
        };

        foreach (var kvp in props.OrderBy(x => x.Key))
        {
            lines.Add($"{kvp.Key}={kvp.Value}");
        }

        await File.WriteAllLinesAsync(path, lines, ct).ConfigureAwait(false);
    }

    private static async Task CopyDirectoryAsync(string sourceDir, string destDir, CancellationToken ct)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            ct.ThrowIfCancellationRequested();
            var destFile = Path.Combine(destDir, Path.GetFileName(file));
            await Task.Run(() => File.Copy(file, destFile, overwrite: true), ct).ConfigureAwait(false);
        }

        foreach (var dir in Directory.GetDirectories(sourceDir))
        {
            ct.ThrowIfCancellationRequested();
            var destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
            await CopyDirectoryAsync(dir, destSubDir, ct).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// Result of file preservation operation.
/// </summary>
public record PreservationResult
{
    public bool Success { get; init; }
    public string? TempPath { get; init; }
    public List<string> PreservedPaths { get; init; } = [];
    public string? Error { get; init; }
}

/// <summary>
/// Result of file restoration operation.
/// </summary>
public record RestorationResult
{
    public bool Success { get; init; }
    public List<string> RestoredPaths { get; init; } = [];
    public List<ConfigConflict> Conflicts { get; init; } = [];
    public string? Error { get; init; }
}

/// <summary>
/// Result of server.properties merge operation.
/// </summary>
public record ServerPropertiesMergeResult
{
    public bool HasConflicts { get; init; }
    public int PreservedCount { get; init; }
}
