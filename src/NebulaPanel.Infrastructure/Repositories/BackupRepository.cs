using Microsoft.EntityFrameworkCore;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Infrastructure.Persistence;

namespace NebulaPanel.Infrastructure.Repositories;

public class BackupRepository(NebulaPanelDbContext context) : IBackupRepository
{
    private readonly NebulaPanelDbContext _context = context;

    public async Task<IReadOnlyList<Backup>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Backups
            .Include(b => b.Server)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Backup>> GetByServerIdAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        return await _context.Backups
            .Include(b => b.Server)
            .Where(b => b.ServerId == serverId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Backup>> GetRecentByServerIdAsync(Guid serverId, int count, CancellationToken cancellationToken = default)
    {
        return await _context.Backups
            .Include(b => b.Server)
            .Where(b => b.ServerId == serverId && b.Status == BackupStatus.Completed)
            .OrderByDescending(b => b.CreatedAt)
            .Take(count)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Backup?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Backups
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Backup?> GetByIdWithServerAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Backups
            .Include(b => b.Server)
            .FirstOrDefaultAsync(b => b.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> GetCountByServerIdAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        return await _context.Backups
            .CountAsync(b => b.ServerId == serverId && b.Status == BackupStatus.Completed, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> GetCountByServerIdAndStatusAsync(Guid serverId, BackupStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.Backups
            .CountAsync(b => b.ServerId == serverId && b.Status == status, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> GetCountByServerIdAndTypeAsync(Guid serverId, BackupType type, CancellationToken cancellationToken = default)
    {
        return await _context.Backups
            .CountAsync(b => b.ServerId == serverId && b.Type == type && b.Status == BackupStatus.Completed, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<long> GetTotalSizeByServerIdAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        return await _context.Backups
            .Where(b => b.ServerId == serverId && b.Status == BackupStatus.Completed)
            .SumAsync(b => b.FileSizeBytes, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Backup?> GetLatestCompletedByServerIdAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        return await _context.Backups
            .Include(b => b.Server)
            .Where(b => b.ServerId == serverId && b.Status == BackupStatus.Completed)
            .OrderByDescending(b => b.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Backup> AddAsync(Backup backup, CancellationToken cancellationToken = default)
    {
        await _context.Backups.AddAsync(backup, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return backup;
    }

    public async Task UpdateAsync(Backup backup, CancellationToken cancellationToken = default)
    {
        _context.Backups.Update(backup);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var backup = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (backup is not null)
        {
            _context.Backups.Remove(backup);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<IReadOnlyList<Backup>> GetBackupsToDeleteForRetentionAsync(
        Guid serverId,
        int keepCount,
        CancellationToken cancellationToken = default)
    {
        return await _context.Backups
            .Where(b => b.ServerId == serverId && b.Status == BackupStatus.Completed)
            .OrderByDescending(b => b.CreatedAt)
            .Skip(keepCount)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> DeleteOldestByServerIdAsync(Guid serverId, int keepCount, CancellationToken cancellationToken = default)
    {
        var backupsToDelete = await GetBackupsToDeleteForRetentionAsync(serverId, keepCount, cancellationToken)
            .ConfigureAwait(false);

        if (backupsToDelete.Count == 0)
        {
            return 0;
        }

        _context.Backups.RemoveRange(backupsToDelete);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return backupsToDelete.Count;
    }
}
