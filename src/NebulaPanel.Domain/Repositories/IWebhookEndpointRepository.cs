using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Domain.Repositories;

public interface IWebhookEndpointRepository
{
    Task<IReadOnlyList<WebhookEndpoint>> GetByOwnerIdAsync(Guid ownerId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WebhookEndpoint>> GetEnabledByEventTypeAsync(WebhookEventType eventType, CancellationToken cancellationToken = default);
    Task<WebhookEndpoint?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(WebhookEndpoint endpoint, CancellationToken cancellationToken = default);
    Task UpdateAsync(WebhookEndpoint endpoint, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
