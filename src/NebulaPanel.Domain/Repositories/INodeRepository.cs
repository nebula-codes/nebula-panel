using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Domain.Repositories;

public interface INodeRepository
{
    Task<IReadOnlyList<Node>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Node>> GetByStatusAsync(NodeStatus status, CancellationToken cancellationToken = default);
    Task<Node?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Node?> GetByIdWithServersAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> NameExistsAsync(string name, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<Node> AddAsync(Node node, CancellationToken cancellationToken = default);
    Task UpdateAsync(Node node, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
