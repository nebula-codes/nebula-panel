using NebulaPanel.Application.Services;

namespace NebulaPanel.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for the backup system.
/// </summary>
public class BackupSettings : IBackupSettings
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Backup";

    /// <summary>
    /// Base directory for storing backups. Defaults to "data/backups".
    /// Can be an absolute path or relative to the application directory.
    /// </summary>
    public string BackupDirectory { get; set; } = "data/backups";

    /// <summary>
    /// Default number of backups to retain per server when using rotation.
    /// </summary>
    public int DefaultRetentionCount { get; set; } = 10;

    /// <summary>
    /// Maximum backup file size in bytes (0 = unlimited).
    /// Backups exceeding this size will fail.
    /// </summary>
    public long MaxBackupSizeBytes { get; set; } = 0;

    /// <summary>
    /// Compression level for backups. Valid values: "Fastest", "Optimal", "NoCompression", "SmallestSize".
    /// </summary>
    public string CompressionLevel { get; set; } = "Optimal";

    /// <summary>
    /// Whether to stop the server before creating a backup.
    /// Stopping ensures data consistency but causes downtime.
    /// </summary>
    public bool StopServerBeforeBackup { get; set; } = false;

    /// <summary>
    /// Whether to create a pre-restore backup before restoring another backup.
    /// This provides a safety net in case the restore needs to be undone.
    /// </summary>
    public bool CreatePreRestoreBackup { get; set; } = true;

    /// <summary>
    /// Default paths to include in selective backups, organized by game slug.
    /// These are relative paths within the server's install directory.
    /// </summary>
    public Dictionary<string, string[]> GameSelectivePaths { get; set; } = new()
    {
        ["minecraft"] =
        [
            "world",
            "world_nether",
            "world_the_end",
            "server.properties",
            "ops.json",
            "whitelist.json",
            "banned-players.json",
            "banned-ips.json"
        ],
        ["valheim"] =
        [
            ".config/unity3d/IronGate/Valheim/worlds_local"
        ],
        ["rust"] =
        [
            "server/rust/player",
            "server/rust/oxide/data"
        ],
        ["terraria"] =
        [
            "Worlds"
        ],
        ["palworld"] =
        [
            "Pal/Saved/SaveGames"
        ],
        ["ark"] =
        [
            "ShooterGame/Saved"
        ],
        ["factorio"] =
        [
            "saves"
        ],
        ["satisfactory"] =
        [
            "FactoryGame/Saved/SaveGames"
        ]
    };

    /// <summary>
    /// Gets the resolved backup directory path.
    /// If the configured path is relative, it's resolved relative to the current directory.
    /// </summary>
    public string GetResolvedBackupDirectory()
    {
        if (Path.IsPathRooted(BackupDirectory))
        {
            return BackupDirectory;
        }

        return Path.Combine(Directory.GetCurrentDirectory(), BackupDirectory);
    }

    /// <summary>
    /// Gets the compression level enum value from the configuration string.
    /// </summary>
    public System.IO.Compression.CompressionLevel GetCompressionLevel()
    {
        return CompressionLevel.ToLowerInvariant() switch
        {
            "fastest" => System.IO.Compression.CompressionLevel.Fastest,
            "nocompression" => System.IO.Compression.CompressionLevel.NoCompression,
            "smallestsize" => System.IO.Compression.CompressionLevel.SmallestSize,
            _ => System.IO.Compression.CompressionLevel.Optimal
        };
    }

    /// <summary>
    /// Gets the default selective paths for a game.
    /// </summary>
    /// <param name="gameSlug">The game's slug/identifier.</param>
    /// <returns>Array of relative paths, or empty array if no defaults configured.</returns>
    public string[] GetSelectivePathsForGame(string gameSlug)
    {
        if (string.IsNullOrEmpty(gameSlug))
        {
            return [];
        }

        return GameSelectivePaths.TryGetValue(gameSlug.ToLowerInvariant(), out var paths)
            ? paths
            : [];
    }
}
