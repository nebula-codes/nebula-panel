namespace NebulaPanel.Shared;

/// <summary>
/// Marker file written by the Updater after a successful update.
/// Read by the main application on startup to handle post-update tasks.
/// </summary>
public sealed record UpdateCompletionMarker
{
    /// <summary>
    /// The version that was installed.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// When the update was completed.
    /// </summary>
    public DateTime CompletedAt { get; init; }

    /// <summary>
    /// The previous version before the update.
    /// </summary>
    public string? FromVersion { get; init; }

    /// <summary>
    /// List of server IDs that should be restarted.
    /// </summary>
    public List<Guid> RestartServerIds { get; init; } = [];

    /// <summary>
    /// Whether any errors occurred during the update.
    /// </summary>
    public bool HadErrors { get; init; }

    /// <summary>
    /// Error messages if any occurred.
    /// </summary>
    public List<string> Errors { get; init; } = [];

    /// <summary>
    /// Whether the health check failed after starting the application.
    /// </summary>
    public bool HealthCheckFailed { get; init; }

    /// <summary>
    /// The update history ID for recording completion.
    /// </summary>
    public Guid? HistoryId { get; init; }

    /// <summary>
    /// Release notes for displaying "What's New" after update.
    /// </summary>
    public string? ReleaseNotes { get; init; }
}
