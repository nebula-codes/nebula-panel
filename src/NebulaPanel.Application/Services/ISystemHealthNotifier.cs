using NebulaPanel.Application.DTOs;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Interface for broadcasting system health updates to connected clients.
/// </summary>
public interface ISystemHealthNotifier
{
    /// <summary>
    /// Notifies all connected clients of updated system health metrics.
    /// </summary>
    Task NotifySystemHealthUpdatedAsync(SystemHealthDto health);
}
