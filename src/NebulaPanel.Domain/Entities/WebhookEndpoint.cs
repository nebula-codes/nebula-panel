using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Domain.Entities;

public class WebhookEndpoint
{
    public Guid Id { get; set; }
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public List<WebhookEventType> SubscribedEvents { get; set; } = [];
    public int FailureCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastDeliveryAt { get; set; }

    private User? _owner;
    public User Owner
    {
        get => _owner ?? throw new InvalidOperationException("Owner navigation property not loaded. Use .Include(x => x.Owner) in your query.");
        set => _owner = value;
    }
    public ICollection<WebhookDelivery> Deliveries { get; set; } = [];
}
