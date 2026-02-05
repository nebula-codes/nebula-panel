namespace NebulaPanel.Infrastructure.Repositories;

using Microsoft.EntityFrameworkCore;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Infrastructure.Persistence;

public class AnnouncementRepository(NebulaPanelDbContext context) : IAnnouncementRepository
{
    public async Task<Announcement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Announcements
            .Include(a => a.CreatedByUser)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Announcement>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        return await context.Announcements
            .Include(a => a.CreatedByUser)
            .Where(a => a.IsActive)
            .Where(a => a.ExpiresAt == null || a.ExpiresAt > now)
            .OrderByDescending(a => a.IsPinned)
            .ThenBy(a => a.DisplayOrder)
            .ThenByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Announcement>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await context.Announcements
            .Include(a => a.CreatedByUser)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task AddAsync(Announcement announcement, CancellationToken cancellationToken = default)
    {
        await context.Announcements.AddAsync(announcement, cancellationToken).ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Announcement announcement, CancellationToken cancellationToken = default)
    {
        context.Announcements.Update(announcement);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await context.Announcements
            .Where(a => a.Id == id)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
