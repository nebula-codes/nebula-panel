namespace NebulaPanel.Domain.Enums;

/// <summary>
/// Defines the scope of a backup operation.
/// </summary>
public enum BackupScope
{
    /// <summary>
    /// Backup the entire server directory.
    /// </summary>
    Full,

    /// <summary>
    /// Backup only specific paths (world, saves, config).
    /// </summary>
    Selective
}
