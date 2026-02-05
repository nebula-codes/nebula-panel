using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Infrastructure.Persistence;

namespace NebulaPanel.Infrastructure.Services;

public class DatabaseInfoService(
    NebulaPanelDbContext context,
    IConfiguration configuration,
    ILogger<DatabaseInfoService> logger) : IDatabaseInfoService
{
    private static DateTime? _lastBackupAt;

    public async Task<DatabaseInfo> GetDatabaseInfoAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        var provider = DetermineProvider(connectionString);
        var filePath = ExtractFilePath(connectionString, provider);
        var sizeBytes = await GetDatabaseSizeAsync(filePath, provider, cancellationToken).ConfigureAwait(false);
        var tableCount = await GetTableCountAsync(cancellationToken).ConfigureAwait(false);

        return new DatabaseInfo(
            Provider: provider,
            ConnectionString: MaskConnectionString(connectionString),
            FilePath: filePath,
            SizeBytes: sizeBytes,
            TableCount: tableCount,
            LastBackupAt: _lastBackupAt);
    }

    public async Task<Stream> CreateBackupAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        var provider = DetermineProvider(connectionString);

        if (provider != "SQLite")
        {
            throw new NotSupportedException($"Database backup is currently only supported for SQLite. Current provider: {provider}");
        }

        var filePath = ExtractFilePath(connectionString, provider);
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            throw new InvalidOperationException("Database file not found.");
        }

        // Checkpoint WAL to ensure all data is in the main file
        await context.Database.ExecuteSqlRawAsync("PRAGMA wal_checkpoint(TRUNCATE);", cancellationToken)
            .ConfigureAwait(false);

        logger.LogInformation("Creating database backup from {FilePath}", filePath);

        // Read the file into a memory stream
        var memoryStream = new MemoryStream();
        await using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            await fileStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
        }

        memoryStream.Position = 0;
        _lastBackupAt = DateTime.UtcNow;

        logger.LogInformation("Database backup created successfully, size: {Size} bytes", memoryStream.Length);

        return memoryStream;
    }

    public string GetBackupFileName()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HH-mm-ss");
        return $"nebula-panel-backup-{timestamp}.db";
    }

    private static string DetermineProvider(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return "Unknown";

        if (connectionString.Contains("Data Source=", StringComparison.OrdinalIgnoreCase) &&
            (connectionString.Contains(".db", StringComparison.OrdinalIgnoreCase) ||
             connectionString.Contains(".sqlite", StringComparison.OrdinalIgnoreCase)))
        {
            return "SQLite";
        }

        if (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
            connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase))
        {
            return "PostgreSQL";
        }

        return "Unknown";
    }

    private static string? ExtractFilePath(string? connectionString, string provider)
    {
        if (string.IsNullOrEmpty(connectionString) || provider != "SQLite")
            return null;

        var parts = connectionString.Split(';');
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed["Data Source=".Length..].Trim();
            }
        }

        return null;
    }

    private static async Task<long> GetDatabaseSizeAsync(string? filePath, string provider, CancellationToken cancellationToken)
    {
        if (provider == "SQLite" && !string.IsNullOrEmpty(filePath) && File.Exists(filePath))
        {
            var fileInfo = new FileInfo(filePath);
            return fileInfo.Length;
        }

        return 0;
    }

    private async Task<int> GetTableCountAsync(CancellationToken cancellationToken)
    {
        try
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");
            var provider = DetermineProvider(connectionString);

            if (provider == "SQLite")
            {
                var count = await context.Database
                    .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' AND name NOT LIKE '__EF%'")
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);
                return count;
            }

            return 0;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get table count");
            return 0;
        }
    }

    private static string? MaskConnectionString(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return null;

        // Mask password if present
        if (connectionString.Contains("Password=", StringComparison.OrdinalIgnoreCase))
        {
            return System.Text.RegularExpressions.Regex.Replace(
                connectionString,
                @"Password=[^;]*",
                "Password=****",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return connectionString;
    }
}
