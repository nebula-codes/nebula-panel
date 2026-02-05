using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Domain.ValueObjects;

/// <summary>
/// Configuration for a mod provider on a specific game.
/// Games can have multiple providers (e.g., Minecraft with Modrinth + CurseForge).
/// </summary>
public class ModProviderConfiguration
{
    public ModProviderType Provider { get; set; }
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; }                               // Lower = higher priority in unified search
    public string? GameSlug { get; set; }                           // Provider-specific game identifier
    public string? GameVersion { get; set; }                        // Default game version filter
    public string ModInstallPath { get; set; } = string.Empty;      // Relative path: "mods/", "plugins/", etc.
    public Dictionary<string, string> ProviderSettings { get; set; } = [];  // Provider-specific config
}
