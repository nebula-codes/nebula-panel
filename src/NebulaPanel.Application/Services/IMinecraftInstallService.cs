using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Service for Minecraft server installation through the wizard.
/// </summary>
public interface IMinecraftInstallService
{
    /// <summary>
    /// Gets all available Minecraft versions.
    /// </summary>
    Task<IReadOnlyList<GameVersionDto>> GetAllVersionsAsync(
        bool includeSnapshots = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets versions filtered by loader type.
    /// </summary>
    Task<IReadOnlyList<GameVersionDto>> GetVersionsForLoaderAsync(
        MinecraftLoader loader,
        bool includeSnapshots = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all Minecraft versions grouped by major version for a specific loader.
    /// </summary>
    Task<IReadOnlyList<string>> GetMinecraftVersionsAsync(
        MinecraftLoader loader,
        bool includeSnapshots = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets available loader builds for a specific Minecraft version.
    /// </summary>
    Task<IReadOnlyList<GameVersionDto>> GetLoaderBuildsAsync(
        MinecraftLoader loader,
        string minecraftVersion,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates wizard data before installation.
    /// </summary>
    Task<Result<IReadOnlyList<string>>> ValidateWizardDataAsync(
        MinecraftServerWizardData data,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a port is available on the local machine.
    /// </summary>
    Task<bool> IsPortAvailableAsync(int port, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates and installs a Minecraft server from wizard data.
    /// Returns the server ID on success.
    /// </summary>
    Task<Result<Guid>> InstallServerAsync(
        MinecraftServerWizardData data,
        Guid ownerId,
        Guid installationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels an ongoing installation.
    /// </summary>
    Task<Result> CancelInstallationAsync(
        Guid installationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets loader metadata for UI display.
    /// </summary>
    IReadOnlyList<MinecraftLoaderInfo> GetLoaderInfos();

    /// <summary>
    /// Gets the default install path for a new Minecraft server.
    /// </summary>
    string GetDefaultInstallPath(string serverName);

    /// <summary>
    /// Generates a random RCON password.
    /// </summary>
    string GenerateRconPassword();
}

/// <summary>
/// Service for writing Minecraft server configuration files.
/// </summary>
public interface IMinecraftConfigWriter
{
    /// <summary>
    /// Creates server.properties file with wizard settings.
    /// Overwrites existing file to ensure settings are applied.
    /// </summary>
    Task WriteServerPropertiesAsync(
        string installPath,
        MinecraftServerPropertiesSettings settings,
        CancellationToken cancellationToken = default);
}
