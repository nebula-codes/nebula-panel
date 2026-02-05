using Microsoft.EntityFrameworkCore;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Infrastructure.Persistence;

namespace NebulaPanel.Infrastructure.Repositories;

public class ScheduledTaskRepository(NebulaPanelDbContext context) : IScheduledTaskRepository
{
    private readonly NebulaPanelDbContext _context = context;

    public async Task<IReadOnlyList<ScheduledTask>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ScheduledTasks
            .Include(t => t.Server)
            .OrderBy(t => t.Server.Name)
            .ThenBy(t => t.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ScheduledTask>> GetByServerIdAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        return await _context.ScheduledTasks
            .Include(t => t.Server)
            .Where(t => t.ServerId == serverId)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ScheduledTask>> GetEnabledTasksAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ScheduledTasks
            .Include(t => t.Server)
            .Where(t => t.IsEnabled && t.CronExpression != null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ScheduledTask?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ScheduledTasks
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ScheduledTask?> GetByIdWithServerAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ScheduledTasks
            .Include(t => t.Server)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ScheduledTasks
            .AnyAsync(t => t.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> NameExistsForServerAsync(string name, Guid serverId, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.ScheduledTasks
            .Where(t => t.Name == name && t.ServerId == serverId);

        if (excludeId.HasValue)
        {
            query = query.Where(t => t.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<ScheduledTask> AddAsync(ScheduledTask task, CancellationToken cancellationToken = default)
    {
        await _context.ScheduledTasks.AddAsync(task, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return task;
    }

    public async Task UpdateAsync(ScheduledTask task, CancellationToken cancellationToken = default)
    {
        _context.ScheduledTasks.Update(task);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var task = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (task is not null)
        {
            _context.ScheduledTasks.Remove(task);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task UpdateLastRunAsync(Guid id, DateTime lastRunAt, DateTime? nextRunAt, CancellationToken cancellationToken = default)
    {
        await _context.ScheduledTasks
            .Where(t => t.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.LastRunAt, lastRunAt)
                .SetProperty(t => t.NextRunAt, nextRunAt),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
