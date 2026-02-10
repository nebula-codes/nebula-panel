using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Infrastructure.Webhooks;

public class HttpWebhookDispatcher(
    IHttpClientFactory httpClientFactory,
    ILogger<HttpWebhookDispatcher> logger) : IWebhookDispatcher
{
    public async Task<WebhookDelivery> DeliverAsync(
        WebhookEndpoint endpoint,
        WebhookEventType eventType,
        string payload,
        CancellationToken cancellationToken = default)
    {
        var delivery = new WebhookDelivery
        {
            Id = Guid.NewGuid(),
            EndpointId = endpoint.Id,
            EventType = eventType,
            Payload = payload,
            AttemptedAt = DateTime.UtcNow,
            AttemptNumber = 1
        };

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var client = httpClientFactory.CreateClient("Webhooks");

            var request = new HttpRequestMessage(HttpMethod.Post, endpoint.Url)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            if (!string.IsNullOrEmpty(endpoint.Secret))
            {
                var signature = ComputeHmacSha256(payload, endpoint.Secret);
                request.Headers.Add("X-Nebula-Signature", signature);
            }
            request.Headers.Add("X-Nebula-Event", eventType.ToString());

            var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            delivery.HttpStatusCode = (int)response.StatusCode;
            delivery.Success = response.IsSuccessStatusCode;
            delivery.DurationMs = (int)stopwatch.ElapsedMilliseconds;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            delivery.Success = false;
            delivery.DurationMs = (int)stopwatch.ElapsedMilliseconds;
            logger.LogWarning(ex, "Webhook delivery to {Url} failed", endpoint.Url);
        }

        return delivery;
    }

    private static string ComputeHmacSha256(string data, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        var hash = HMACSHA256.HashData(keyBytes, dataBytes);
        return $"sha256={Convert.ToHexStringLower(hash)}";
    }
}
