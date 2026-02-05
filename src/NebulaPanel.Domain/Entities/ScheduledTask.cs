using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Domain.Entities;

/// <summary>
/// Represents a scheduled task for a game server (restart, backup, command, etc.).
/// </summary>
public class ScheduledTask
{
    public Guid Id { get; set; }
    public Guid ServerId { get; set; }
    private GameServer? _server;
    public GameServer Server
    {
        get => _server ?? throw new InvalidOperationException("Server navigation property was not loaded. Use .Include(x => x.Server) in your query.");
        set => _server = value;
    }

    public string Name { get; set; } = string.Empty;
    public ScheduledTaskType TaskType { get; set; }
    public string? CronExpression { get; set; }                 // Cron schedule (null for one-time)
    public DateTime? NextRunAt { get; set; }
    public DateTime? LastRunAt { get; set; }
    public bool IsEnabled { get; set; } = true;

    // Task-specific configuration (JSON)
    public string? Configuration { get; set; }
}
