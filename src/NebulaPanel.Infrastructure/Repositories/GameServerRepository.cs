using Microsoft.EntityFrameworkCore;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Exceptions;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Infrastructure.Persistence;

namespace NebulaPanel.Infrastructure.Repositories;

public class GameServerRepository(NebulaPanelDbContext context) : IGameServerRepository
{
    private readonly NebulaPanelDbContext _context = context;

    public async Task<IReadOnlyList<GameServer>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.GameServers
            .Include(s => s.Game)
            .Include(s => s.Owner)
            .Include(s => s.Node)
            .OrderByDescending(s => s.IsPinned)
            .ThenBy(s => s.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<GameServer>> GetByOwnerIdAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        return await _context.GameServers
            .Include(s => s.Game)
            .Include(s => s.Node)
            .Where(s => s.OwnerId == ownerId)
            .OrderByDescending(s => s.IsPinned)
            .ThenBy(s => s.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<GameServer>> GetByStatusAsync(ServerStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.GameServers
            .Include(s => s.Game)
            .Include(s => s.Node)
            .Where(s => s.Status == status)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<GameServer>> GetByNodeIdAndStatusAsync(Guid nodeId, ServerStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.GameServers
            .Where(s => s.NodeId == nodeId && s.Status == status)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<GameServer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.GameServers
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<GameServer?> GetByIdWithGameAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.GameServers
            .Include(s => s.Game)
            .Include(s => s.Owner)
            .Include(s => s.Node)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.GameServers
            .AnyAsync(s => s.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> NameExistsForOwnerAsync(string name, Guid ownerId, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.GameServers
            .Where(s => s.Name == name && s.OwnerId == ownerId);

        if (excludeId.HasValue)
        {
            query = query.Where(s => s.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> IsPortInUseAsync(int port, string bindAddress, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.GameServers
            .Where(s => s.PrimaryPort == port && s.BindAddress == bindAddress);

        if (excludeId.HasValue)
        {
            query = query.Where(s => s.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<GameServer> AddAsync(GameServer server, CancellationToken cancellationToken = default)
    {
        await _context.GameServers.AddAsync(server, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return server;
    }

    public async Task UpdateAsync(GameServer server, CancellationToken cancellationToken = default)
    {
        var entry = _context.Entry(server);
        if (entry.State == EntityState.Detached)
        {
            // Check if another instance with the same key is already tracked
            var tracked = _context.ChangeTracker.Entries<GameServer>()
                .FirstOrDefault(e => e.Entity.Id == server.Id);

            if (tracked != null)
            {
                // Update the already-tracked entity's values instead of attaching a new one
                tracked.CurrentValues.SetValues(server);
            }
            else
            {
                _context.GameServers.Update(server);
            }
        }
        // If already tracked (not detached), EF Core will save the changes automatically
        try
        {
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyException("A concurrency conflict occurred while updating the game server.", ex);
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var server = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (server is not null)
        {
            _context.GameServers.Remove(server);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
