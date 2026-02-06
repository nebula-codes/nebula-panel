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

    public User Owner { get; set; } = null!;
    public ICollection<WebhookDelivery> Deliveries { get; set; } = [];
}
