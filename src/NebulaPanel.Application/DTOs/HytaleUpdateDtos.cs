namespace NebulaPanel.Application.DTOs;

/// <summary>
/// Phases during a Hytale server update operation.
/// </summary>
public enum HytaleUpdatePhase
{
    Initializing,
    StoppingServer,
    CreatingBackup,
    Authenticating,
    Downloading,
    Extracting,
    PreservingFiles,
    Configuring,
    StartingServer,
    Completed,
    Failed
}

/// <summary>
/// Result of checking for a Hytale server update.
/// </summary>
public record HytaleServerUpdateCheckResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public bool UpdateAvailable { get; init; }
    public string? CurrentVersion { get; init; }
    public string? LatestVersion { get; init; }
    public string? Patchline { get; init; }
    public DateTime CheckedAt { get; init; }

    public static HytaleServerUpdateCheckResult Succeeded(
        bool updateAvailable,
        string currentVersion,
        string? latestVersion,
        string patchline) => new()
    {
        Success = true,
        UpdateAvailable = updateAvailable,
        CurrentVersion = currentVersion,
        LatestVersion = latestVersion,
        Patchline = patchline,
        CheckedAt = DateTime.UtcNow
    };

    public static HytaleServerUpdateCheckResult Failed(string error) => new()
    {
        Success = false,
        Error = error,
        CheckedAt = DateTime.UtcNow
    };
}

/// <summary>
/// Result of updating a Hytale server.
/// </summary>
public record HytaleUpdateResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public string? PreviousVersion { get; init; }
    public string? NewVersion { get; init; }
    public Guid? BackupId { get; init; }

    public static HytaleUpdateResult Succeeded(
        string previousVersion,
        string newVersion,
        Guid? backupId = null) => new()
    {
        Success = true,
        PreviousVersion = previousVersion,
        NewVersion = newVersion,
        BackupId = backupId
    };

    public static HytaleUpdateResult Failed(string error) => new()
    {
        Success = false,
        Error = error
    };
}

/// <summary>
/// Options for a Hytale server update operation.
/// </summary>
public record HytaleUpdateOptions
{
    /// <summary>
    /// Whether to create a backup before updating.
    /// </summary>
    public bool CreateBackupBeforeUpdate { get; init; } = true;

    /// <summary>
    /// Whether to preserve world data during update.
    /// </summary>
    public bool PreserveWorldData { get; init; } = true;

    /// <summary>
    /// Whether to preserve custom configurations during update.
    /// </summary>
    public bool PreserveCustomConfigs { get; init; } = true;

    /// <summary>
    /// Whether to restart the server after update if it was running.
    /// </summary>
    public bool RestartServerAfterUpdate { get; init; } = true;
}

/// <summary>
/// Progress information for a Hytale update operation.
/// </summary>
public record HytaleUpdateProgressDto
{
    public required Guid ServerId { get; init; }
    public required HytaleUpdatePhase Phase { get; init; }
    public required string Message { get; init; }
    public double Percentage { get; init; }
    public long? BytesDownloaded { get; init; }
    public long? TotalBytes { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public bool IsComplete { get; init; }
    public bool HasError { get; init; }
    public string? ErrorMessage { get; init; }

    public static HytaleUpdateProgressDto Create(
        Guid serverId,
        HytaleUpdatePhase phase,
        string message,
        double percentage) => new()
    {
        ServerId = serverId,
        Phase = phase,
        Message = message,
        Percentage = percentage,
        Timestamp = DateTime.UtcNow
    };

    public static HytaleUpdateProgressDto Downloading(
        Guid serverId,
        double percentage,
        long? bytesDownloaded,
        long? totalBytes) => new()
    {
        ServerId = serverId,
        Phase = HytaleUpdatePhase.Downloading,
        Message = "Downloading new version...",
        Percentage = 20 + (percentage * 0.5), // Download is 20-70% of total
        BytesDownloaded = bytesDownloaded,
        TotalBytes = totalBytes,
        Timestamp = DateTime.UtcNow
    };

    public static HytaleUpdateProgressDto Complete(Guid serverId) => new()
    {
        ServerId = serverId,
        Phase = HytaleUpdatePhase.Completed,
        Message = "Update complete!",
        Percentage = 100,
        IsComplete = true,
        Timestamp = DateTime.UtcNow
    };

    public static HytaleUpdateProgressDto Error(Guid serverId, string error) => new()
    {
        ServerId = serverId,
        Phase = HytaleUpdatePhase.Failed,
        Message = error,
        Percentage = 0,
        HasError = true,
        ErrorMessage = error,
        Timestamp = DateTime.UtcNow
    };
}

/// <summary>
/// Cached update status for a Hytale server.
/// </summary>
public record HytaleUpdateStatusDto
{
    public bool IsHytaleServer { get; init; }
    public bool UpdateAvailable { get; init; }
    public string? CurrentVersion { get; init; }
    public string? AvailableVersion { get; init; }
    public string? Patchline { get; init; }
    public DateTime? InstalledAt { get; init; }
    public DateTime? LastCheckedAt { get; init; }
}
