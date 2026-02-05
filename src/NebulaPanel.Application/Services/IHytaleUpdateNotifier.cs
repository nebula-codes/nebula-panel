using NebulaPanel.Application.DTOs;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Notifier for broadcasting Hytale server update progress via SignalR.
/// </summary>
public interface IHytaleUpdateNotifier
{
    /// <summary>
    /// Notify clients of update progress.
    /// </summary>
    /// <param name="serverId">The server being updated.</param>
    /// <param name="progress">Progress information.</param>
    /// <param name="ct">Cancellation token.</param>
    Task NotifyProgressAsync(
        Guid serverId,
        HytaleUpdateProgressDto progress,
        CancellationToken ct = default);

    /// <summary>
    /// Notify clients that the update completed.
    /// </summary>
    /// <param name="serverId">The server that was updated.</param>
    /// <param name="success">Whether the update was successful.</param>
    /// <param name="newVersion">The new version if successful.</param>
    /// <param name="error">Error message if failed.</param>
    /// <param name="ct">Cancellation token.</param>
    Task NotifyCompleteAsync(
        Guid serverId,
        bool success,
        string? newVersion,
        string? error,
        CancellationToken ct = default);

    /// <summary>
    /// Notify clients that an update is available.
    /// </summary>
    /// <param name="serverId">The server with the available update.</param>
    /// <param name="currentVersion">Current installed version.</param>
    /// <param name="availableVersion">Available version.</param>
    /// <param name="ct">Cancellation token.</param>
    Task NotifyUpdateAvailableAsync(
        Guid serverId,
        string currentVersion,
        string availableVersion,
        CancellationToken ct = default);
}
