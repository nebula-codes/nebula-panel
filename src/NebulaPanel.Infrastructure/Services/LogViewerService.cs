namespace NebulaPanel.Infrastructure.Services;

using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Services;

/// <summary>
/// Service for viewing application log files from the file system.
/// </summary>
public partial class LogViewerService(
    IConfiguration configuration,
    ILogger<LogViewerService> logger) : ILogViewerService
{
    private readonly string _logDirectory = GetLogDirectory(configuration);

    private static string GetLogDirectory(IConfiguration configuration)
    {
        // Extract directory from the Serilog file path configuration
        var path = configuration["Serilog:WriteTo:1:Args:path"];
        if (string.IsNullOrEmpty(path))
            return "data/logs";

        // Remove the filename pattern to get the directory
        var directory = Path.GetDirectoryName(path);
        return string.IsNullOrEmpty(directory) ? "data/logs" : directory;
    }

    public Task<IReadOnlyList<LogFileInfo>> GetLogFilesAsync(CancellationToken ct = default)
    {
        var dir = new DirectoryInfo(_logDirectory);
        if (!dir.Exists)
        {
            logger.LogDebug("Log directory does not exist: {Directory}", _logDirectory);
            return Task.FromResult<IReadOnlyList<LogFileInfo>>(Array.Empty<LogFileInfo>());
        }

        var files = dir.GetFiles("nebula-*.log")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Select(f => new LogFileInfo(f.Name, f.Length, f.LastWriteTimeUtc))
            .ToList();

        logger.LogDebug("Found {Count} log files in {Directory}", files.Count, _logDirectory);
        return Task.FromResult<IReadOnlyList<LogFileInfo>>(files);
    }

    public async Task<LogFileContent> ReadLogFileAsync(
        string fileName,
        int skip = 0,
        int take = 500,
        string? levelFilter = null,
        string? searchTerm = null,
        CancellationToken ct = default)
    {
        // Validate filename to prevent path traversal
        if (fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\'))
        {
            throw new ArgumentException("Invalid file name", nameof(fileName));
        }

        var filePath = Path.Combine(_logDirectory, fileName);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Log file not found: {fileName}");
        }

        var entries = new List<LogEntry>();
        var lineNumber = 0;
        var filteredCount = 0;
        StringBuilder? currentException = null;
        LogEntry? currentEntry = null;

        // Use FileShare.ReadWrite to allow reading while Serilog is writing
        await using var fileStream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite);
        using var reader = new StreamReader(fileStream);

        while (await reader.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
        {
            lineNumber++;

            // Try to parse as a new log entry
            var entry = ParseLogLine(lineNumber, line);

            if (entry is not null)
            {
                // Save the previous entry if it exists and passes filters
                if (currentEntry is not null)
                {
                    var finalEntry = currentException is not null
                        ? currentEntry with { Exception = currentException.ToString() }
                        : currentEntry;

                    if (MatchesFilters(finalEntry, levelFilter, searchTerm))
                    {
                        filteredCount++;
                        if (filteredCount > skip && entries.Count < take)
                        {
                            entries.Add(finalEntry);
                        }
                    }
                }

                currentEntry = entry;
                currentException = null;
            }
            else if (currentEntry is not null)
            {
                // This is a continuation line (likely an exception stack trace)
                currentException ??= new StringBuilder();
                currentException.AppendLine(line);
            }
        }

        // Don't forget the last entry
        if (currentEntry is not null)
        {
            var finalEntry = currentException is not null
                ? currentEntry with { Exception = currentException.ToString() }
                : currentEntry;

            if (MatchesFilters(finalEntry, levelFilter, searchTerm))
            {
                filteredCount++;
                if (filteredCount > skip && entries.Count < take)
                {
                    entries.Add(finalEntry);
                }
            }
        }

        return new LogFileContent(
            fileName,
            entries,
            lineNumber,
            filteredCount,
            filteredCount > skip + take);
    }

    public Task<bool> DeleteLogFileAsync(string fileName, CancellationToken ct = default)
    {
        // Validate filename to prevent path traversal
        if (fileName.Contains("..") || fileName.Contains('/') || fileName.Contains('\\'))
        {
            throw new ArgumentException("Invalid file name", nameof(fileName));
        }

        var filePath = Path.Combine(_logDirectory, fileName);
        if (!File.Exists(filePath))
        {
            return Task.FromResult(false);
        }

        try
        {
            File.Delete(filePath);
            logger.LogInformation("Deleted log file: {FileName}", fileName);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete log file: {FileName}", fileName);
            return Task.FromResult(false);
        }
    }

    private static bool MatchesFilters(LogEntry entry, string? levelFilter, string? searchTerm)
    {
        if (!string.IsNullOrEmpty(levelFilter) &&
            !entry.Level.Equals(levelFilter, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(searchTerm))
        {
            var searchLower = searchTerm.ToLowerInvariant();
            if (!entry.Message.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) &&
                !entry.Source.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) &&
                !(entry.Exception?.Contains(searchTerm, StringComparison.OrdinalIgnoreCase) ?? false))
            {
                return false;
            }
        }

        return true;
    }

    // Log format: 2024-01-15 10:30:45.123 +00:00 [INF] SourceContext: Message
    [GeneratedRegex(@"^(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2}) \[(\w{3})\] ([^:]+): (.*)$")]
    private static partial Regex LogLineRegex();

    private static LogEntry? ParseLogLine(int lineNumber, string line)
    {
        var match = LogLineRegex().Match(line);
        if (!match.Success)
            return null;

        if (!DateTime.TryParse(match.Groups[1].Value, out var timestamp))
            return null;

        return new LogEntry(
            lineNumber,
            timestamp,
            match.Groups[2].Value,
            match.Groups[3].Value.Trim(),
            match.Groups[4].Value,
            null);
    }
}
