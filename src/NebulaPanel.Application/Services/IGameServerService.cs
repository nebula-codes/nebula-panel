using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Application.Services;

public interface IGameServerService
{
    Task<IReadOnlyList<GameServerListItemDto>> GetAllServersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GameServerListItemDto>> GetServersByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default);
    Task<Result<GameServerDto>> GetServerByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<GameServerDto>> CreateServerAsync(CreateGameServerRequest request, Guid ownerId, CancellationToken cancellationToken = default);
    Task<Result<GameServerDto>> UpdateServerAsync(Guid id, UpdateGameServerRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a game server from the database.
    /// </summary>
    /// <param name="id">The server ID.</param>
    /// <param name="deleteFiles">If true, also deletes the server files from disk.</param>
    /// <param name="deleteContainer">If true, also removes the Docker container (if applicable).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<Result> DeleteServerAsync(Guid id, bool deleteFiles = false, bool deleteContainer = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Installs game server files using SteamCMD for Steam-based games.
    /// </summary>
    /// <param name="serverId">The server ID to install files for.</param>
    /// <param name="branch">Optional beta branch name.</param>
    /// <param name="betaPassword">Optional beta branch password.</param>
    /// <param name="progress">Progress reporter for tracking installation progress.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<Result> InstallServerAsync(
        Guid serverId,
        string? branch = null,
        string? betaPassword = null,
        IProgress<InstallProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates game server files using SteamCMD for Steam-based games.
    /// </summary>
    /// <param name="serverId">The server ID to update.</param>
    /// <param name="branch">Optional beta branch name.</param>
    /// <param name="betaPassword">Optional beta branch password.</param>
    /// <param name="progress">Progress reporter for tracking update progress.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<Result> UpdateServerAsync(
        Guid serverId,
        string? branch = null,
        string? betaPassword = null,
        IProgress<UpdateProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the server's game supports SteamCMD installation.
    /// </summary>
    /// <param name="serverId">The server ID to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the game has a Steam App ID configured.</returns>
    Task<Result<bool>> SupportsSteamInstallAsync(Guid serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets information about the installed Steam app for a server.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Steam app info if available.</returns>
    Task<Result<SteamAppInfo?>> GetSteamAppInfoAsync(Guid serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a game server.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<Result> StartServerAsync(Guid serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops a game server gracefully.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<Result> StopServerAsync(Guid serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restarts a game server (stop then start).
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<Result> RestartServerAsync(Guid serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Force kills a game server without graceful shutdown.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<Result> KillServerAsync(Guid serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current resource usage for a running server.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resource usage information.</returns>
    Task<Result<ResourceUsage>> GetResourceUsageAsync(Guid serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a command to a running server.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="command">The command to send.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with optional response string (for RCON commands).</returns>
    Task<Result<string?>> SendCommandAsync(Guid serverId, string command, CancellationToken cancellationToken = default);

    /// <summary>
    /// Streams console output from a running server.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Async enumerable of console output lines.</returns>
    IAsyncEnumerable<string> StreamConsoleAsync(Guid serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the tags for a server.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="tags">The new list of tags.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<Result<List<string>>> UpdateTagsAsync(Guid serverId, List<string> tags, CancellationToken cancellationToken = default);

    /// <summary>
    /// Toggles the pinned/favorited state of a server.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result with the new pinned state.</returns>
    Task<Result<bool>> TogglePinAsync(Guid serverId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Recreates the Docker container for a server. Server must be stopped.
    /// Useful when container settings need to be updated.
    /// </summary>
    /// <param name="serverId">The server ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    Task<Result> RecreateContainerAsync(Guid serverId, CancellationToken cancellationToken = default);
}
