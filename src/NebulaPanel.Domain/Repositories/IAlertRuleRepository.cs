using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Domain.Repositories;

public interface IAlertRuleRepository
{
    Task<IReadOnlyList<AlertRule>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlertRule>> GetByOwnerIdAsync(Guid ownerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlertRule>> GetByServerIdAsync(Guid serverId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlertRule>> GetEnabledAsync(CancellationToken cancellationToken = default);
    Task<AlertRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(AlertRule rule, CancellationToken cancellationToken = default);
    Task UpdateAsync(AlertRule rule, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
