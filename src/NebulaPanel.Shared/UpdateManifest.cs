namespace NebulaPanel.Shared;

/// <summary>
/// Manifest describing a pending update operation.
/// Written by UpdateService, read by NebulaPanel.Updater.
/// </summary>
public sealed record UpdateManifest
{
    /// <summary>
    /// The version being installed.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Full path to the downloaded update package (.tar.gz).
    /// </summary>
    public required string DownloadPath { get; init; }

    /// <summary>
    /// ID of the pre-update backup, or null if no backup was created.
    /// </summary>
    public string? BackupId { get; init; }

    /// <summary>
    /// Whether game servers were stopped before the update.
    /// </summary>
    public bool StopGameServers { get; init; }

    /// <summary>
    /// Whether game servers should be restarted after the update completes.
    /// </summary>
    public bool RestartGameServers { get; init; }

    /// <summary>
    /// List of server IDs that were running and should be restarted after update.
    /// </summary>
    public List<Guid> RestartServerIds { get; init; } = [];

    /// <summary>
    /// When the update was initiated.
    /// </summary>
    public DateTime Timestamp { get; init; }
}
