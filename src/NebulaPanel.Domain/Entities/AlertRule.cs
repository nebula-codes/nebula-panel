using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Domain.Entities;

public class AlertRule
{
    public Guid Id { get; set; }
    public Guid? ServerId { get; set; }
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public AlertResourceType ResourceType { get; set; }
    public AlertComparison Comparison { get; set; }
    public double Threshold { get; set; }
    public int DurationSeconds { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int CooldownMinutes { get; set; } = 15;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastTriggeredAt { get; set; }

    public GameServer? Server { get; set; }
    private User? _owner;
    public User Owner
    {
        get => _owner ?? throw new InvalidOperationException("Owner navigation property not loaded. Use .Include(x => x.Owner) in your query.");
        set => _owner = value;
    }
}
