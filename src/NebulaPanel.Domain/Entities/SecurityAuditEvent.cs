namespace NebulaPanel.Domain.Entities;

using NebulaPanel.Domain.Enums;

/// <summary>
/// Records security-related events for audit purposes.
/// </summary>
public class SecurityAuditEvent
{
    public Guid Id { get; set; }

    /// <summary>
    /// The ID of the user involved in the event, if known.
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// The username attempted or used, stored separately for failed login attempts
    /// where the user may not exist.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// The type of security event.
    /// </summary>
    public SecurityEventType EventType { get; set; }

    /// <summary>
    /// The IP address from which the event originated.
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// The User-Agent header from the request.
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Additional details about the event (e.g., error message, lockout duration).
    /// </summary>
    public string? Details { get; set; }

    /// <summary>
    /// Whether the operation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// When the event occurred.
    /// </summary>
    public DateTime OccurredAt { get; set; }

    // Navigation property
    public User? User { get; set; }
}
