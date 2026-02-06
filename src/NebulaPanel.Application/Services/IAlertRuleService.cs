using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;

namespace NebulaPanel.Application.Services;

public interface IAlertRuleService
{
    Task<IReadOnlyList<AlertRuleDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AlertRuleDto>> GetByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default);
    Task<Result<AlertRuleDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<AlertRuleDto>> CreateAsync(CreateAlertRuleRequest request, Guid ownerId, CancellationToken cancellationToken = default);
    Task<Result<AlertRuleDto>> UpdateAsync(Guid id, UpdateAlertRuleRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
