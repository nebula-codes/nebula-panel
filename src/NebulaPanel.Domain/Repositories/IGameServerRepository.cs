using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Domain.Repositories;

public interface IGameServerRepository
{
    Task<IReadOnlyList<GameServer>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GameServer>> GetByOwnerIdAsync(Guid ownerId, CancellationToken cancellationToken = default);
    Task<GameServer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<GameServer?> GetByIdWithGameAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> NameExistsForOwnerAsync(string name, Guid ownerId, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<bool> IsPortInUseAsync(int port, string bindAddress, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<GameServer> AddAsync(GameServer server, CancellationToken cancellationToken = default);
    Task UpdateAsync(GameServer server, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
