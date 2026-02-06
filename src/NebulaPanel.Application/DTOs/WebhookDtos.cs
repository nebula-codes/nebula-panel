using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Application.DTOs;

public record WebhookEndpointDto(
    Guid Id,
    Guid OwnerId,
    string Name,
    string Url,
    bool IsEnabled,
    IReadOnlyList<WebhookEventType> SubscribedEvents,
    int FailureCount,
    DateTime CreatedAt,
    DateTime? LastDeliveryAt);

public record CreateWebhookEndpointRequest(
    string Name,
    string Url,
    IReadOnlyList<WebhookEventType> SubscribedEvents);

public record UpdateWebhookEndpointRequest(
    string Name,
    string Url,
    bool IsEnabled,
    IReadOnlyList<WebhookEventType> SubscribedEvents);

public record WebhookDeliveryDto(
    Guid Id,
    WebhookEventType EventType,
    int? HttpStatusCode,
    bool Success,
    DateTime AttemptedAt,
    int DurationMs,
    int AttemptNumber);
