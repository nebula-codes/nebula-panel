using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Application.Services;

public interface IWebhookService
{
    Task<IReadOnlyList<WebhookEndpointDto>> GetByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default);
    Task<Result<WebhookEndpointDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<WebhookEndpointDto>> CreateAsync(CreateWebhookEndpointRequest request, Guid ownerId, CancellationToken cancellationToken = default);
    Task<Result<WebhookEndpointDto>> UpdateAsync(Guid id, UpdateWebhookEndpointRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WebhookDeliveryDto>> GetDeliveriesAsync(Guid endpointId, CancellationToken cancellationToken = default);
    Task DispatchEventAsync(WebhookEventType eventType, object payload, CancellationToken cancellationToken = default);
    Task<Result> TestWebhookAsync(Guid endpointId, CancellationToken cancellationToken = default);
}
