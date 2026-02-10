using Microsoft.EntityFrameworkCore;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Infrastructure.Persistence;

namespace NebulaPanel.Infrastructure.Repositories;

public class NodeRepository(NebulaPanelDbContext context) : INodeRepository
{
    private readonly NebulaPanelDbContext _context = context;

    public async Task<IReadOnlyList<Node>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Nodes
            .Include(n => n.Servers)
            .OrderBy(n => n.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Node>> GetByStatusAsync(NodeStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.Nodes
            .Where(n => n.Status == status)
            .OrderBy(n => n.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Node?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Nodes
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<Node?> GetByIdWithServersAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Nodes
            .Include(n => n.Servers)
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Nodes
            .AnyAsync(n => n.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Nodes.Where(n => n.Name == name);
        if (excludeId.HasValue)
            query = query.Where(n => n.Id != excludeId.Value);

        return await query.AnyAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Node> AddAsync(Node node, CancellationToken cancellationToken = default)
    {
        _context.Nodes.Add(node);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return node;
    }

    public async Task UpdateAsync(Node node, CancellationToken cancellationToken = default)
    {
        _context.Nodes.Update(node);
        await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var node = await _context.Nodes
            .FirstOrDefaultAsync(n => n.Id == id, cancellationToken)
            .ConfigureAwait(false);

        if (node is not null)
        {
            _context.Nodes.Remove(node);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
