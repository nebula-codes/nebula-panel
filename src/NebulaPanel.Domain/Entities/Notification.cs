using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Domain.Entities;

/// <summary>
/// Represents a notification sent to a user.
/// </summary>
public class Notification
{
    /// <summary>
    /// Unique identifier for the notification.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The ID of the user this notification belongs to.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// The type of notification.
    /// </summary>
    public NotificationType Type { get; set; }

    /// <summary>
    /// Short title for the notification.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Full message content of the notification.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Whether this notification has been read by the user.
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// When the notification was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the notification was read, if applicable.
    /// </summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>
    /// Optional URL to navigate to when the notification is clicked.
    /// </summary>
    public string? ActionUrl { get; set; }

    /// <summary>
    /// Optional ID of a related entity (e.g., server ID, backup ID).
    /// </summary>
    public Guid? RelatedEntityId { get; set; }

    /// <summary>
    /// Optional JSON metadata for additional context.
    /// </summary>
    public string? Metadata { get; set; }

    // Navigation properties

    /// <summary>
    /// The user this notification belongs to.
    /// </summary>
    public User User { get; set; } = null!;
}
