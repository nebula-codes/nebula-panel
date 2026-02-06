using Microsoft.EntityFrameworkCore;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Infrastructure.Persistence;

namespace NebulaPanel.Infrastructure.Repositories;

public class AlertRuleRepository(NebulaPanelDbContext context) : IAlertRuleRepository
{
    public async Task<IReadOnlyList<AlertRule>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await context.AlertRules
            .Include(r => r.Server)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AlertRule>> GetByOwnerIdAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        return await context.AlertRules
            .Include(r => r.Server)
            .Where(r => r.OwnerId == ownerId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AlertRule>> GetByServerIdAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        return await context.AlertRules
            .Include(r => r.Server)
            .Where(r => r.ServerId == serverId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<AlertRule>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        return await context.AlertRules
            .Include(r => r.Server)
            .Where(r => r.IsEnabled)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<AlertRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.AlertRules
            .Include(r => r.Server)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(AlertRule rule, CancellationToken cancellationToken = default)
    {
        await context.AlertRules.AddAsync(rule, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(AlertRule rule, CancellationToken cancellationToken = default)
    {
        context.AlertRules.Update(rule);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await context.AlertRules
            .Where(r => r.Id == id)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
