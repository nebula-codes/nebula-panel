namespace NebulaPanel.Domain.ValueObjects;

/// <summary>
/// Options for applying an update.
/// </summary>
public record UpdateOptions
{
    /// <summary>
    /// Whether to create a backup before applying the update.
    /// </summary>
    public bool CreateBackup { get; init; } = true;

    /// <summary>
    /// Whether to stop game servers before applying the update.
    /// </summary>
    public bool StopGameServers { get; init; }

    /// <summary>
    /// Whether to restart game servers after the update is applied.
    /// </summary>
    public bool RestartGameServers { get; init; } = true;

    /// <summary>
    /// Additional paths to include in the backup.
    /// </summary>
    public List<string> AdditionalBackupPaths { get; init; } = [];
}
