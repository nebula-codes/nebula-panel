using Microsoft.EntityFrameworkCore;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Infrastructure.Persistence;

namespace NebulaPanel.Infrastructure.Repositories;

public class ServerModRepository(NebulaPanelDbContext context) : IServerModRepository
{
    private readonly NebulaPanelDbContext _context = context;

    public async Task<IReadOnlyList<ServerMod>> GetByServerIdAsync(
        Guid serverId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ServerMods
            .Where(m => m.ServerId == serverId)
            .OrderBy(m => m.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ServerMod?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.ServerMods
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ServerMod?> GetByServerAndModAsync(
        Guid serverId,
        ModProviderType provider,
        string modId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ServerMods
            .FirstOrDefaultAsync(
                m => m.ServerId == serverId && m.Provider == provider && m.ModId == modId,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<ServerMod> AddAsync(ServerMod mod, CancellationToken cancellationToken = default)
    {
        await _context.ServerMods.AddAsync(mod, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return mod;
    }

    public async Task UpdateAsync(ServerMod mod, CancellationToken cancellationToken = default)
    {
        _context.ServerMods.Update(mod);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var mod = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (mod is not null)
        {
            _context.ServerMods.Remove(mod);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<bool> ExistsAsync(
        Guid serverId,
        ModProviderType provider,
        string modId,
        CancellationToken cancellationToken = default)
    {
        return await _context.ServerMods
            .AnyAsync(
                m => m.ServerId == serverId && m.Provider == provider && m.ModId == modId,
                cancellationToken)
            .ConfigureAwait(false);
    }
}
