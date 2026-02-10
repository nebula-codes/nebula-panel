namespace NebulaPanel.Domain.Entities;

using NebulaPanel.Domain.Enums;

public class UserActivity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    private User? _user;
    public User User
    {
        get => _user ?? throw new InvalidOperationException("User navigation property not loaded. Use .Include(x => x.User) in your query.");
        set => _user = value;
    }
    public DateTime Timestamp { get; set; }
    public UserActivityType ActivityType { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Metadata { get; set; }
}
