using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Domain.Entities;

/// <summary>
/// Represents a tracked activity or event for a game server.
/// </summary>
public class ServerActivity
{
    public Guid Id { get; set; }

    /// <summary>
    /// The server this activity is associated with.
    /// </summary>
    public Guid ServerId { get; set; }
    private GameServer? _server;
    public GameServer Server
    {
        get => _server ?? throw new InvalidOperationException("Server navigation property was not loaded. Use .Include(x => x.Server) in your query.");
        set => _server = value;
    }

    /// <summary>
    /// The user who initiated this activity. Null for system-initiated events.
    /// </summary>
    public Guid? UserId { get; set; }
    public User? User { get; set; }

    /// <summary>
    /// When the activity occurred.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// The type of activity.
    /// </summary>
    public ServerActivityType ActivityType { get; set; }

    /// <summary>
    /// Human-readable description of the activity.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Optional JSON metadata for additional context.
    /// </summary>
    public string? Metadata { get; set; }
}
