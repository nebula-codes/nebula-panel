using Microsoft.EntityFrameworkCore;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Infrastructure.Persistence;

namespace NebulaPanel.Infrastructure.Repositories;

public class GameRepository(NebulaPanelDbContext context) : IGameRepository
{
    private readonly NebulaPanelDbContext _context = context;

    public async Task<IReadOnlyList<Game>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Games
            .OrderBy(g => g.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Game?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Games
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Game?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        return await _context.Games
            .FirstOrDefaultAsync(g => g.Slug == slug.ToLowerInvariant(), cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Games
            .AnyAsync(g => g.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> SlugExistsAsync(string slug, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Games.Where(g => g.Slug == slug.ToLowerInvariant());

        if (excludeId.HasValue)
        {
            query = query.Where(g => g.Id != excludeId.Value);
        }

        return await query.AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Game> AddAsync(Game game, CancellationToken cancellationToken = default)
    {
        await _context.Games.AddAsync(game, cancellationToken).ConfigureAwait(false);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return game;
    }

    public async Task UpdateAsync(Game game, CancellationToken cancellationToken = default)
    {
        _context.Games.Update(game);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var game = await GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (game is not null)
        {
            _context.Games.Remove(game);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<int> GetServerCountAsync(Guid gameId, CancellationToken cancellationToken = default)
    {
        return await _context.GameServers
            .CountAsync(s => s.GameId == gameId, cancellationToken)
            .ConfigureAwait(false);
    }
}
