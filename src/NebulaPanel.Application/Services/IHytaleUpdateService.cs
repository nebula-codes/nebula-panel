using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Service for managing Hytale server updates.
/// </summary>
public interface IHytaleUpdateService
{
    /// <summary>
    /// Check for available updates for a Hytale server.
    /// </summary>
    /// <param name="server">The server to check for updates.</param>
    /// <param name="userId">The user ID requesting the check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing update availability information.</returns>
    Task<HytaleServerUpdateCheckResult> CheckForUpdateAsync(
        GameServer server,
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Update the server to the latest version.
    /// </summary>
    /// <param name="server">The server to update.</param>
    /// <param name="userId">The user ID requesting the update.</param>
    /// <param name="options">Update options (backup, preserve files, etc.).</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing update outcome.</returns>
    Task<HytaleUpdateResult> UpdateServerAsync(
        GameServer server,
        Guid userId,
        HytaleUpdateOptions options,
        IProgress<HytaleUpdateProgressDto>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Change the server's patchline (e.g., from release to pre-release).
    /// This will download the latest version from the new patchline.
    /// </summary>
    /// <param name="server">The server to update.</param>
    /// <param name="newPatchline">The new patchline ("release" or "pre-release").</param>
    /// <param name="userId">The user ID requesting the change.</param>
    /// <param name="options">Update options.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing update outcome.</returns>
    Task<HytaleUpdateResult> ChangePatchlineAsync(
        GameServer server,
        string newPatchline,
        Guid userId,
        HytaleUpdateOptions options,
        IProgress<HytaleUpdateProgressDto>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Dismiss the update notification for a specific version.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="version">The version to dismiss.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DismissUpdateAsync(
        Guid serverId,
        string version,
        CancellationToken ct = default);

    /// <summary>
    /// Get cached update status for a server.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Cached update status.</returns>
    Task<HytaleUpdateStatusDto> GetUpdateStatusAsync(
        Guid serverId,
        CancellationToken ct = default);

    /// <summary>
    /// Get the active update progress for a server if an update is currently running.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Current progress if an update is active, null otherwise.</returns>
    Task<HytaleUpdateProgressDto?> GetActiveUpdateProgressAsync(
        Guid serverId,
        CancellationToken ct = default);
}
