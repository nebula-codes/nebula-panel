namespace NebulaPanel.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    // Account lockout fields
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockoutEndTime { get; set; }
    public DateTime? LastFailedLoginAt { get; set; }

    /// <summary>
    /// Returns true if the account is currently locked out.
    /// </summary>
    public bool IsLockedOut => LockoutEndTime.HasValue && LockoutEndTime.Value > DateTime.UtcNow;

    // Navigation properties
    public ICollection<UserRole> Roles { get; set; } = [];
    public ICollection<GameServer> OwnedServers { get; set; } = [];
    public ICollection<ServerPermission> ServerPermissions { get; set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];

    // Hytale credentials (one-to-one, optional)
    public HytaleUserCredentials? HytaleCredentials { get; set; }

    // Hytale preferences (one-to-one, optional)
    public HytaleUserPreferences? HytalePreferences { get; set; }

    // Notifications
    public ICollection<Notification> Notifications { get; set; } = [];
}
