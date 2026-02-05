namespace NebulaPanel.Application.Services;

/// <summary>
/// Service for viewing application log files.
/// </summary>
public interface ILogViewerService
{
    /// <summary>
    /// Gets a list of available log files.
    /// </summary>
    Task<IReadOnlyList<LogFileInfo>> GetLogFilesAsync(CancellationToken ct = default);

    /// <summary>
    /// Reads log entries from a specific log file with optional filtering.
    /// </summary>
    Task<LogFileContent> ReadLogFileAsync(
        string fileName,
        int skip = 0,
        int take = 500,
        string? levelFilter = null,
        string? searchTerm = null,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a log file.
    /// </summary>
    Task<bool> DeleteLogFileAsync(string fileName, CancellationToken ct = default);
}

/// <summary>
/// Information about a log file.
/// </summary>
public record LogFileInfo(
    string FileName,
    long SizeBytes,
    DateTime LastModified);

/// <summary>
/// Content of a log file with pagination info.
/// </summary>
public record LogFileContent(
    string FileName,
    IReadOnlyList<LogEntry> Entries,
    int TotalLines,
    int FilteredCount,
    bool HasMore);

/// <summary>
/// A single log entry.
/// </summary>
public record LogEntry(
    int LineNumber,
    DateTime Timestamp,
    string Level,
    string Source,
    string Message,
    string? Exception);
