using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Application.Services;

public interface IWebhookDispatcher
{
    Task<WebhookDelivery> DeliverAsync(WebhookEndpoint endpoint, WebhookEventType eventType, string payload, CancellationToken cancellationToken = default);
}
