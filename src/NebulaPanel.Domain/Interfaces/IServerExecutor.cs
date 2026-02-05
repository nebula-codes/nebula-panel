using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Domain.Interfaces;

/// <summary>
/// Abstraction for different server execution strategies (Docker, Native, SteamCMD).
/// </summary>
public interface IServerExecutor
{
    /// <summary>
    /// The deployment type this executor handles.
    /// </summary>
    ServerDeploymentType DeploymentType { get; }

    /// <summary>
    /// Installs the game server files.
    /// </summary>
    Task<bool> InstallAsync(
        GameServer server,
        IProgress<InstallProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Updates the game server to the latest version.
    /// </summary>
    Task<bool> UpdateAsync(
        GameServer server,
        IProgress<UpdateProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Starts the game server.
    /// </summary>
    Task<bool> StartAsync(GameServer server, CancellationToken ct = default);

    /// <summary>
    /// Stops the game server.
    /// </summary>
    /// <param name="server">The server to stop.</param>
    /// <param name="force">If true, forcefully kills the process without graceful shutdown.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> StopAsync(GameServer server, bool force = false, CancellationToken ct = default);

    /// <summary>
    /// Restarts the game server (stop then start).
    /// </summary>
    Task<bool> RestartAsync(GameServer server, CancellationToken ct = default);

    /// <summary>
    /// Gets the current status of the server.
    /// </summary>
    Task<ServerStatus> GetStatusAsync(GameServer server, CancellationToken ct = default);

    /// <summary>
    /// Streams console output from the server.
    /// </summary>
    IAsyncEnumerable<string> StreamConsoleAsync(GameServer server, CancellationToken ct = default);

    /// <summary>
    /// Sends a command to the server via stdin.
    /// </summary>
    Task SendCommandAsync(GameServer server, string command, CancellationToken ct = default);

    /// <summary>
    /// Gets current resource usage for the server.
    /// </summary>
    Task<ResourceUsage> GetResourceUsageAsync(GameServer server, CancellationToken ct = default);

    /// <summary>
    /// Cleans up resources associated with the server (e.g., removes Docker container).
    /// For native servers, this is typically a no-op.
    /// </summary>
    Task CleanupAsync(GameServer server, CancellationToken ct = default);
}
