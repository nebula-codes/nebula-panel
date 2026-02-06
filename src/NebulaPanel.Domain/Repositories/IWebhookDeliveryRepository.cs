using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Domain.Repositories;

public interface IWebhookDeliveryRepository
{
    Task<IReadOnlyList<WebhookDelivery>> GetByEndpointIdAsync(Guid endpointId, int limit = 50, CancellationToken cancellationToken = default);
    Task AddAsync(WebhookDelivery delivery, CancellationToken cancellationToken = default);
    Task DeleteOlderThanAsync(DateTime threshold, CancellationToken cancellationToken = default);
}
