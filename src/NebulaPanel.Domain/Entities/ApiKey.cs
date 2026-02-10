namespace NebulaPanel.Domain.Entities;

public class ApiKey
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public string KeyPrefix { get; set; } = string.Empty;
    public List<string> Scopes { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    private User? _user;
    public User User
    {
        get => _user ?? throw new InvalidOperationException("User navigation property not loaded. Use .Include(x => x.User) in your query.");
        set => _user = value;
    }

    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsExpired => ExpiresAt.HasValue && ExpiresAt.Value < DateTime.UtcNow;
    public bool IsActive => !IsRevoked && !IsExpired;
}
