namespace NebulaPanel.Domain.Repositories;

using NebulaPanel.Domain.Entities;

public interface IPermissionRepository
{
    Task<Permission?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Permission?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<Permission>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Permission>> GetByCategoryAsync(string category, CancellationToken ct = default);
    Task AddAsync(Permission permission, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<Permission> permissions, CancellationToken ct = default);
}
