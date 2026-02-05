namespace NebulaPanel.Domain.Repositories;

using NebulaPanel.Domain.Entities;

public interface IServerPermissionRepository
{
    Task<ServerPermission?> GetAsync(Guid userId, Guid serverId, string permissionCode, CancellationToken ct = default);
    Task<IReadOnlyList<ServerPermission>> GetByUserAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<ServerPermission>> GetByServerAsync(Guid serverId, CancellationToken ct = default);
    Task<IReadOnlyList<ServerPermission>> GetByUserAndServerAsync(Guid userId, Guid serverId, CancellationToken ct = default);
    Task AddAsync(ServerPermission permission, CancellationToken ct = default);
    Task UpdateAsync(ServerPermission permission, CancellationToken ct = default);
    Task DeleteAsync(ServerPermission permission, CancellationToken ct = default);
    Task DeleteByUserAndServerAsync(Guid userId, Guid serverId, CancellationToken ct = default);
}
