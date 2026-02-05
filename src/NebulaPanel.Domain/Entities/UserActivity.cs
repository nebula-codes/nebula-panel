namespace NebulaPanel.Domain.Entities;

using NebulaPanel.Domain.Enums;

public class UserActivity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime Timestamp { get; set; }
    public UserActivityType ActivityType { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Metadata { get; set; }
}
