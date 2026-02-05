using NebulaPanel.Application.DTOs;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Service for monitoring host system resources (CPU, memory, disk).
/// </summary>
public interface IHostMonitorService
{
    /// <summary>
    /// Gets the current system health metrics.
    /// </summary>
    Task<SystemHealthDto> GetCurrentHealthAsync(CancellationToken cancellationToken = default);
}
