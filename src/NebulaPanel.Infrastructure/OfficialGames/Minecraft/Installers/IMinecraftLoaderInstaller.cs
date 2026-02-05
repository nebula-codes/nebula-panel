using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;
using NebulaPanel.Infrastructure.OfficialGames.Minecraft.Models;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft.Installers;

/// <summary>
/// Interface for Minecraft loader-specific installation logic.
/// Each loader (Vanilla, Paper, Fabric, etc.) implements this interface.
/// </summary>
public interface IMinecraftLoaderInstaller
{
    /// <summary>
    /// The loader type this installer handles.
    /// </summary>
    MinecraftLoader LoaderType { get; }

    /// <summary>
    /// Installs the server files to the specified directory.
    /// </summary>
    /// <param name="context">Installation context with server info and version details.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure with details.</returns>
    Task<LoaderInstallResult> InstallAsync(
        MinecraftInstallContext context,
        IProgress<OfficialInstallProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the startup command configuration for this loader.
    /// </summary>
    /// <param name="context">Installation context with version details.</param>
    /// <returns>Startup command information.</returns>
    StartupCommandInfo GetStartupCommand(MinecraftInstallContext context);
}
