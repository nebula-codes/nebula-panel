using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Repositories;

namespace NebulaPanel.Application.Services;

public class WebhookService(
    IWebhookEndpointRepository endpointRepository,
    IWebhookDeliveryRepository deliveryRepository,
    IWebhookDispatcher dispatcher,
    ILogger<WebhookService> logger) : IWebhookService
{
    private readonly IWebhookEndpointRepository _endpointRepository = endpointRepository;
    private readonly IWebhookDeliveryRepository _deliveryRepository = deliveryRepository;
    private readonly IWebhookDispatcher _dispatcher = dispatcher;
    private readonly ILogger<WebhookService> _logger = logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<IReadOnlyList<WebhookEndpointDto>> GetByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        var endpoints = await _endpointRepository.GetByOwnerIdAsync(ownerId, cancellationToken).ConfigureAwait(false);
        return endpoints.Select(MapToDto).ToList();
    }

    public async Task<Result<WebhookEndpointDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var endpoint = await _endpointRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (endpoint is null)
            return Result.Failure<WebhookEndpointDto>(Error.NotFound("Webhook endpoint", id.ToString()));
        return MapToDto(endpoint);
    }

    public async Task<Result<WebhookEndpointDto>> CreateAsync(CreateWebhookEndpointRequest request, Guid ownerId, CancellationToken cancellationToken = default)
    {
        var secret = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

        var endpoint = new WebhookEndpoint
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Name = request.Name,
            Url = request.Url,
            Secret = secret,
            IsEnabled = true,
            SubscribedEvents = request.SubscribedEvents.ToList(),
            CreatedAt = DateTime.UtcNow
        };

        await _endpointRepository.AddAsync(endpoint, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Created webhook endpoint {EndpointName} ({EndpointId}) for owner {OwnerId}", endpoint.Name, endpoint.Id, ownerId);

        return MapToDto(endpoint);
    }

    public async Task<Result<WebhookEndpointDto>> UpdateAsync(Guid id, UpdateWebhookEndpointRequest request, CancellationToken cancellationToken = default)
    {
        var endpoint = await _endpointRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (endpoint is null)
            return Result.Failure<WebhookEndpointDto>(Error.NotFound("Webhook endpoint", id.ToString()));

        endpoint.Name = request.Name;
        endpoint.Url = request.Url;
        endpoint.IsEnabled = request.IsEnabled;
        endpoint.SubscribedEvents = request.SubscribedEvents.ToList();

        if (!request.IsEnabled)
            endpoint.FailureCount = 0;

        await _endpointRepository.UpdateAsync(endpoint, cancellationToken).ConfigureAwait(false);
        return MapToDto(endpoint);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var endpoint = await _endpointRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (endpoint is null)
            return Result.Failure(Error.NotFound("Webhook endpoint", id.ToString()));

        await _endpointRepository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        return Result.Success();
    }

    public async Task<IReadOnlyList<WebhookDeliveryDto>> GetDeliveriesAsync(Guid endpointId, CancellationToken cancellationToken = default)
    {
        var deliveries = await _deliveryRepository.GetByEndpointIdAsync(endpointId, cancellationToken: cancellationToken).ConfigureAwait(false);
        return deliveries.Select(d => new WebhookDeliveryDto(
            d.Id, d.EventType, d.HttpStatusCode, d.Success, d.AttemptedAt, d.DurationMs, d.AttemptNumber
        )).ToList();
    }

    public async Task DispatchEventAsync(WebhookEventType eventType, object payload, CancellationToken cancellationToken = default)
    {
        var endpoints = await _endpointRepository.GetEnabledByEventTypeAsync(eventType, cancellationToken).ConfigureAwait(false);
        if (endpoints.Count == 0)
            return;

        var json = JsonSerializer.Serialize(new { @event = eventType.ToString(), data = payload, timestamp = DateTime.UtcNow }, JsonOptions);

        foreach (var endpoint in endpoints)
        {
            try
            {
                await DeliverAndRecordAsync(endpoint, eventType, json, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deliver webhook to {EndpointUrl} for event {EventType}", endpoint.Url, eventType);
            }
        }
    }

    public async Task<Result> TestWebhookAsync(Guid endpointId, CancellationToken cancellationToken = default)
    {
        var endpoint = await _endpointRepository.GetByIdAsync(endpointId, cancellationToken).ConfigureAwait(false);
        if (endpoint is null)
            return Result.Failure(Error.NotFound("Webhook endpoint", endpointId.ToString()));

        var json = JsonSerializer.Serialize(new { @event = "test", data = new { message = "Webhook test from Nebula Panel" }, timestamp = DateTime.UtcNow }, JsonOptions);

        var delivery = await DeliverAndRecordAsync(endpoint, WebhookEventType.ServerStarted, json, cancellationToken).ConfigureAwait(false);
        return delivery.Success ? Result.Success() : Result.Failure($"Webhook delivery failed with HTTP {delivery.HttpStatusCode}");
    }

    private async Task<WebhookDelivery> DeliverAndRecordAsync(WebhookEndpoint endpoint, WebhookEventType eventType, string payload, CancellationToken cancellationToken)
    {
        var delivery = await _dispatcher.DeliverAsync(endpoint, eventType, payload, cancellationToken).ConfigureAwait(false);

        if (delivery.Success)
        {
            endpoint.FailureCount = 0;
            endpoint.LastDeliveryAt = DateTime.UtcNow;
        }
        else
        {
            endpoint.FailureCount++;
            if (endpoint.FailureCount >= 10)
            {
                endpoint.IsEnabled = false;
                _logger.LogWarning("Webhook endpoint {EndpointName} disabled after {FailureCount} consecutive failures", endpoint.Name, endpoint.FailureCount);
            }
        }

        await _deliveryRepository.AddAsync(delivery, cancellationToken).ConfigureAwait(false);
        await _endpointRepository.UpdateAsync(endpoint, cancellationToken).ConfigureAwait(false);

        return delivery;
    }

    private static WebhookEndpointDto MapToDto(WebhookEndpoint endpoint) => new(
        endpoint.Id,
        endpoint.OwnerId,
        endpoint.Name,
        endpoint.Url,
        endpoint.IsEnabled,
        endpoint.SubscribedEvents,
        endpoint.FailureCount,
        endpoint.CreatedAt,
        endpoint.LastDeliveryAt);
}
