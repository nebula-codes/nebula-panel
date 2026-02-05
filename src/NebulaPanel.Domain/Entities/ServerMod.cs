using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Domain.Entities;

public class ServerMod
{
    public Guid Id { get; set; }
    public Guid ServerId { get; set; }
    public string ModId { get; set; } = string.Empty;           // Provider-specific ID
    public string Name { get; set; } = string.Empty;
    public string? Version { get; set; }                        // Display version
    public string? VersionId { get; set; }                      // Provider-specific version ID
    public ModProviderType Provider { get; set; }
    public DateTime InstalledAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string? LocalPath { get; set; }                      // Path within server directory
    public string? Author { get; set; }                         // Mod author name
    public string? IconUrl { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? FileHash { get; set; }                       // For integrity verification

    // Navigation property
    private GameServer? _server;
    public GameServer Server
    {
        get => _server ?? throw new InvalidOperationException("Server navigation property was not loaded. Use .Include(x => x.Server) in your query.");
        set => _server = value;
    }
}
