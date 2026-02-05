using Microsoft.EntityFrameworkCore;

namespace NebulaPanel.Infrastructure.Persistence;

public static class SqliteOptimizations
{
    /// <summary>
    /// Applies SQLite-specific optimizations for better performance and concurrency.
    /// Call this after ensuring migrations have been applied.
    /// </summary>
    public static async Task ApplySqliteOptimizationsAsync(
        this NebulaPanelDbContext context,
        CancellationToken cancellationToken = default)
    {
        if (!context.Database.IsSqlite())
        {
            return;
        }

        // Enable WAL mode for better concurrency
        await context.Database.ExecuteSqlRawAsync(
            "PRAGMA journal_mode=WAL;", cancellationToken).ConfigureAwait(false);

        // Set synchronous to NORMAL for better performance
        await context.Database.ExecuteSqlRawAsync(
            "PRAGMA synchronous=NORMAL;", cancellationToken).ConfigureAwait(false);

        // Increase cache size (negative value = KB, so -64000 = 64MB)
        await context.Database.ExecuteSqlRawAsync(
            "PRAGMA cache_size=-64000;", cancellationToken).ConfigureAwait(false);

        // Enable foreign keys
        await context.Database.ExecuteSqlRawAsync(
            "PRAGMA foreign_keys=ON;", cancellationToken).ConfigureAwait(false);
    }
}
