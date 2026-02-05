using NebulaPanel.Application.DTOs;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Interface for broadcasting server relocation progress via SignalR.
/// </summary>
public interface IServerRelocationNotifier
{
    /// <summary>
    /// Notifies clients of relocation progress.
    /// </summary>
    Task NotifyProgressAsync(
        Guid relocationId,
        Guid serverId,
        ServerRelocationProgressDto progress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Notifies clients that relocation is complete.
    /// </summary>
    Task NotifyCompleteAsync(
        Guid relocationId,
        Guid serverId,
        bool success,
        string? error,
        CancellationToken cancellationToken = default);
}
