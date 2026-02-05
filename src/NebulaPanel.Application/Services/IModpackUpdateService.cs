using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Service for checking and applying modpack updates.
/// </summary>
public interface IModpackUpdateService
{
    /// <summary>
    /// Check if an update is available for the server's installed modpack.
    /// </summary>
    /// <param name="server">The game server with an installed modpack.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Update information if available, null if no update or no modpack installed.</returns>
    Task<ModpackUpdateInfo?> CheckForUpdateAsync(
        GameServer server,
        CancellationToken ct = default);

    /// <summary>
    /// Get detailed information about available versions for comparison.
    /// </summary>
    /// <param name="server">The game server with an installed modpack.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of available versions newer than the installed version.</returns>
    Task<IReadOnlyList<ModpackVersionSummary>> GetAvailableVersionsAsync(
        GameServer server,
        CancellationToken ct = default);

    /// <summary>
    /// Update the server's modpack to the specified version.
    /// </summary>
    /// <param name="server">The game server to update.</param>
    /// <param name="targetVersionId">The version ID to update to.</param>
    /// <param name="options">Update options (backup, preserve, etc.).</param>
    /// <param name="progress">Progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result of the update operation.</returns>
    Task<ModpackUpdateResult> UpdateModpackAsync(
        GameServer server,
        string targetVersionId,
        ModpackUpdateOptions options,
        IProgress<ModpackUpdateProgressDto>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Dismiss an update notification for a specific version.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="versionId">The version ID to dismiss.</param>
    /// <param name="ct">Cancellation token.</param>
    Task DismissUpdateAsync(
        Guid serverId,
        string versionId,
        CancellationToken ct = default);

    /// <summary>
    /// Get cached update status for a server.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Update status information.</returns>
    Task<ModpackUpdateStatusDto> GetUpdateStatusAsync(
        Guid serverId,
        CancellationToken ct = default);
}
