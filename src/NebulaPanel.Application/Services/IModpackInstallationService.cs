using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Service for orchestrating modpack installation on game servers.
/// </summary>
public interface IModpackInstallationService
{
    /// <summary>
    /// Installs a modpack on the specified game server.
    /// </summary>
    /// <param name="server">The game server to install the modpack on.</param>
    /// <param name="request">The installation request parameters.</param>
    /// <param name="progress">Optional progress reporter for tracking installation status.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The result of the installation operation.</returns>
    Task<ModpackInstallResult> InstallAsync(
        GameServer server,
        ModpackInstallRequest request,
        IProgress<ModpackInstallProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Uninstalls the currently installed modpack from the server.
    /// </summary>
    /// <param name="server">The game server to uninstall the modpack from.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if uninstallation was successful.</returns>
    Task<bool> UninstallAsync(GameServer server, CancellationToken ct = default);

    /// <summary>
    /// Updates the installed modpack to a new version.
    /// </summary>
    /// <param name="server">The game server to update.</param>
    /// <param name="newVersionId">The new version identifier to install.</param>
    /// <param name="progress">Optional progress reporter for tracking update status.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if update was successful.</returns>
    Task<bool> UpdateAsync(
        GameServer server,
        string newVersionId,
        IProgress<ModpackInstallProgress>? progress = null,
        CancellationToken ct = default);
}
