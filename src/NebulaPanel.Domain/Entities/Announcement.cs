using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Domain.Entities;

/// <summary>
/// Represents an admin-configurable announcement displayed on the dashboard.
/// </summary>
public class Announcement
{
    public Guid Id { get; set; }

    /// <summary>
    /// The announcement title.
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// The announcement content. Supports Markdown formatting.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// The type/severity of the announcement.
    /// </summary>
    public AnnouncementType Type { get; set; }

    /// <summary>
    /// Whether the announcement is currently active and visible.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether the announcement is pinned to the top.
    /// </summary>
    public bool IsPinned { get; set; }

    /// <summary>
    /// When the announcement was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// When the announcement should automatically expire. Null for no expiration.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// The user who created this announcement.
    /// </summary>
    public Guid CreatedByUserId { get; set; }
    private User? _createdByUser;
    public User CreatedByUser
    {
        get => _createdByUser ?? throw new InvalidOperationException("CreatedByUser navigation property was not loaded. Use .Include(x => x.CreatedByUser) in your query.");
        set => _createdByUser = value;
    }

    /// <summary>
    /// Display order for sorting. Lower values appear first.
    /// </summary>
    public int DisplayOrder { get; set; }
}
