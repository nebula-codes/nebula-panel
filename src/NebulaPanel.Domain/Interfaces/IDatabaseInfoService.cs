namespace NebulaPanel.Domain.Interfaces;

public interface IDatabaseInfoService
{
    Task<DatabaseInfo> GetDatabaseInfoAsync(CancellationToken cancellationToken = default);
    Task<Stream> CreateBackupAsync(CancellationToken cancellationToken = default);
    string GetBackupFileName();
}

public record DatabaseInfo(
    string Provider,
    string? ConnectionString,
    string? FilePath,
    long SizeBytes,
    int TableCount,
    DateTime? LastBackupAt);
