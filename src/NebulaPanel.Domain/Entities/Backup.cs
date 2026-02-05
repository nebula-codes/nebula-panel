using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Domain.Entities;

/// <summary>
/// Represents a backup of a game server.
/// </summary>
public class Backup
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
    public string FilePath { get; set; } = string.Empty;        // Path to backup file
    public long FileSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    public BackupType Type { get; set; }
    public BackupStatus Status { get; set; }
    public string? Notes { get; set; }

    // Backup scope and content tracking
    public BackupScope Scope { get; set; }                      // Full or Selective
    public string? SourcePaths { get; set; }                    // JSON array of relative paths backed up

    // Optional link to scheduled task that created this backup
    public Guid? ScheduledTaskId { get; set; }

    // What's included in the backup (for quick filtering)
    public bool IncludesWorld { get; set; } = true;
    public bool IncludesConfig { get; set; } = true;
    public bool IncludesMods { get; set; }
}
