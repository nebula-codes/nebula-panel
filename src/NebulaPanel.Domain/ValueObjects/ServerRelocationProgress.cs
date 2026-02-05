namespace NebulaPanel.Domain.ValueObjects;

/// <summary>
/// Represents the state of a server relocation operation.
/// </summary>
public enum ServerRelocationState
{
    Validating,
    CountingFiles,
    CopyingFiles,
    UpdatingConfiguration,
    RecreatingContainer,
    CleaningUp,
    Completed,
    Failed,
    RollingBack
}

/// <summary>
/// Represents progress during server relocation.
/// </summary>
public record ServerRelocationProgress
{
    public ServerRelocationState State { get; init; }
    public double PercentComplete { get; init; }
    public string CurrentStep { get; init; } = string.Empty;
    public string? CurrentFile { get; init; }
    public long BytesCopied { get; init; }
    public long TotalBytes { get; init; }
    public int FilesCopied { get; init; }
    public int TotalFiles { get; init; }
    public string? ErrorMessage { get; init; }
    public bool IsIndeterminate { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    public static ServerRelocationProgress Validating() => new()
    {
        State = ServerRelocationState.Validating,
        CurrentStep = "Validating relocation...",
        IsIndeterminate = true
    };

    public static ServerRelocationProgress CountingFiles() => new()
    {
        State = ServerRelocationState.CountingFiles,
        CurrentStep = "Counting files...",
        IsIndeterminate = true
    };

    public static ServerRelocationProgress CopyingFiles(string? file, long copied, long total, int filesCopied, int totalFiles) => new()
    {
        State = ServerRelocationState.CopyingFiles,
        PercentComplete = total > 0 ? (copied * 100.0 / total) : 0,
        CurrentStep = "Copying files...",
        CurrentFile = file,
        BytesCopied = copied,
        TotalBytes = total,
        FilesCopied = filesCopied,
        TotalFiles = totalFiles
    };

    public static ServerRelocationProgress UpdatingConfiguration() => new()
    {
        State = ServerRelocationState.UpdatingConfiguration,
        PercentComplete = 90,
        CurrentStep = "Updating server configuration...",
        IsIndeterminate = true
    };

    public static ServerRelocationProgress RecreatingContainer() => new()
    {
        State = ServerRelocationState.RecreatingContainer,
        PercentComplete = 92,
        CurrentStep = "Preparing container recreation...",
        IsIndeterminate = true
    };

    public static ServerRelocationProgress CleaningUp() => new()
    {
        State = ServerRelocationState.CleaningUp,
        PercentComplete = 95,
        CurrentStep = "Cleaning up old files...",
        IsIndeterminate = true
    };

    public static ServerRelocationProgress Completed() => new()
    {
        State = ServerRelocationState.Completed,
        PercentComplete = 100,
        CurrentStep = "Relocation complete"
    };

    public static ServerRelocationProgress Failed(string error) => new()
    {
        State = ServerRelocationState.Failed,
        CurrentStep = "Relocation failed",
        ErrorMessage = error
    };

    public static ServerRelocationProgress RollingBack() => new()
    {
        State = ServerRelocationState.RollingBack,
        CurrentStep = "Rolling back changes...",
        IsIndeterminate = true
    };
}
