using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Domain.Interfaces;

/// <summary>
/// Provider interface for official game definitions and installation.
/// Each implementation handles a specific official game (Minecraft, Valheim, etc.).
/// </summary>
public interface IOfficialGameProvider
{
    /// <summary>
    /// Unique slug identifying this game (e.g., "minecraft", "valheim").
    /// </summary>
    string GameSlug { get; }

    /// <summary>
    /// Gets the static game definition (name, configs, mod providers, etc.).
    /// This is used to seed/update the Game entity in the database.
    /// </summary>
    GameDefinition GetGameDefinition();

    /// <summary>
    /// Gets available versions for installation/update.
    /// </summary>
    Task<IReadOnlyList<GameVersionInfo>> GetAvailableVersionsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Installs the game server to the specified path with the given version.
    /// </summary>
    Task<ServerInstallationResult> InstallServerAsync(
        GameServer server,
        string version,
        IProgress<OfficialInstallProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing server to a new version.
    /// </summary>
    Task<ServerInstallationResult> UpdateServerAsync(
        GameServer server,
        string targetVersion,
        IProgress<OfficialInstallProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the default install path for a new server of this game type.
    /// </summary>
    string GetDefaultInstallPath(string serverName);
}
