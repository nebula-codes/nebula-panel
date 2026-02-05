using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Domain.Repositories;

public interface ISystemSettingsRepository
{
    Task<SystemSettings> GetAsync(CancellationToken cancellationToken = default);
    Task UpdateAsync(SystemSettings settings, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(CancellationToken cancellationToken = default);
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
