using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Domain.Repositories;

public interface IScheduledTaskRepository
{
    Task<IReadOnlyList<ScheduledTask>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScheduledTask>> GetByServerIdAsync(Guid serverId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScheduledTask>> GetEnabledTasksAsync(CancellationToken cancellationToken = default);
    Task<ScheduledTask?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ScheduledTask?> GetByIdWithServerAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> NameExistsForServerAsync(string name, Guid serverId, Guid? excludeId = null, CancellationToken cancellationToken = default);
    Task<ScheduledTask> AddAsync(ScheduledTask task, CancellationToken cancellationToken = default);
    Task UpdateAsync(ScheduledTask task, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateLastRunAsync(Guid id, DateTime lastRunAt, DateTime? nextRunAt, CancellationToken cancellationToken = default);
}
