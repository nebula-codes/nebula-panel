using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;

namespace NebulaPanel.Application.Services;

public interface IApiKeyService
{
    Task<IReadOnlyList<ApiKeyDto>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<ApiKeyCreatedDto>> CreateAsync(CreateApiKeyRequest request, Guid userId, CancellationToken cancellationToken = default);
    Task<Result> RevokeAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<Result<Guid>> ValidateAsync(string rawKey, CancellationToken cancellationToken = default);
}
