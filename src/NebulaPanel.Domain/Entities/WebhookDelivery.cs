using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Domain.Entities;

public class WebhookDelivery
{
    public Guid Id { get; set; }
    public Guid EndpointId { get; set; }
    public WebhookEventType EventType { get; set; }
    public string Payload { get; set; } = string.Empty;
    public int? HttpStatusCode { get; set; }
    public bool Success { get; set; }
    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
    public int DurationMs { get; set; }
    public int AttemptNumber { get; set; } = 1;

    private WebhookEndpoint? _endpoint;
    public WebhookEndpoint Endpoint
    {
        get => _endpoint ?? throw new InvalidOperationException("Endpoint navigation property not loaded. Use .Include(x => x.Endpoint) in your query.");
        set => _endpoint = value;
    }
}
