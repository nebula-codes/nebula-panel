namespace NebulaPanel.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Infrastructure.Persistence;

public class SystemSettingsRepository(NebulaPanelDbContext context) : ISystemSettingsRepository
{
    public async Task<SystemSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await context.SystemSettings
            .Include(s => s.LastModifiedByUser)
            .FirstOrDefaultAsync(s => s.Id == SystemSettings.SingletonId, cancellationToken)
            .ConfigureAwait(false);

        if (settings is null)
        {
            settings = new SystemSettings();
            await context.SystemSettings.AddAsync(settings, cancellationToken).ConfigureAwait(false);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return settings;
    }

    public async Task UpdateAsync(SystemSettings settings, CancellationToken cancellationToken = default)
    {
        settings.LastModifiedAt = DateTime.UtcNow;
        context.SystemSettings.Update(settings);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
    {
        return await context.SystemSettings
            .AnyAsync(s => s.Id == SystemSettings.SingletonId, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var exists = await ExistsAsync(cancellationToken).ConfigureAwait(false);
        if (!exists)
        {
            var settings = new SystemSettings();
            await context.SystemSettings.AddAsync(settings, cancellationToken).ConfigureAwait(false);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
