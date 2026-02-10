using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Domain.Entities;

public class GameServer
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconPath { get; set; }  // Server-specific icon (overrides game icon)
    public Guid GameId { get; set; }
    private Game? _game;
    public Game Game
    {
        get => _game ?? throw new InvalidOperationException("Game navigation property was not loaded. Use .Include(x => x.Game) in your query.");
        set => _game = value;
    }

    // Deployment Configuration
    public ServerDeploymentType DeploymentType { get; set; }    // Docker, Native
    public string InstallPath { get; set; } = string.Empty;     // Base path for server files
    public string? DockerContainerId { get; set; }
    public DockerConfiguration? DockerConfig { get; set; }
    public NativeConfiguration? NativeConfig { get; set; }

    // RCON Configuration (per-server override of game defaults)
    public RconConfiguration? RconConfig { get; set; }

    // Network Configuration
    public int PrimaryPort { get; set; }
    public Dictionary<string, int> AdditionalPorts { get; set; } = [];  // "rcon": 25575, "query": 25565
    public string BindAddress { get; set; } = "0.0.0.0";

    // Version Information
    public string? InstalledVersion { get; set; }   // Steam build ID or version string

    // Modpack Information
    public InstalledModpackInfo? InstalledModpack { get; set; }

    // Hytale Server Information
    public HytaleServerInfo? HytaleInfo { get; set; }

    // Pinning / Favorites
    public bool IsPinned { get; set; }

    // Tags for organization
    public List<string> Tags { get; set; } = [];

    // Runtime State (not persisted, or cached)
    public ServerStatus Status { get; set; }
    public DateTime? LastStarted { get; set; }
    public DateTime? LastStopped { get; set; }
    public int? ProcessId { get; set; }

    // Concurrency control for optimistic locking
    public byte[] RowVersion { get; set; } = [];

    // Resource Limits
    public ResourceLimits? ResourceLimits { get; set; }

    // Node assignment (null = local/manager node)
    public Guid? NodeId { get; set; }
    public Node? Node { get; set; }

    // Relationships
    public Guid OwnerId { get; set; }
    private User? _owner;
    public User Owner
    {
        get => _owner ?? throw new InvalidOperationException("Owner navigation property was not loaded. Use .Include(x => x.Owner) in your query.");
        set => _owner = value;
    }
    public ICollection<ServerMod> InstalledMods { get; set; } = [];
    public ICollection<ScheduledTask> ScheduledTasks { get; set; } = [];
    public ICollection<Backup> Backups { get; set; } = [];

    // Helper to determine command sending method
    public CommandMethod PreferredCommandMethod => RconConfig?.Enabled == true
        ? CommandMethod.Rcon
        : CommandMethod.Stdin;
}
