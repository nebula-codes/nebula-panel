using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Service for relocating game server installations to new directories.
/// </summary>
public interface IServerRelocationService
{
    /// <summary>
    /// Validates if a server can be relocated to a new path.
    /// Checks permissions, disk space, and server status.
    /// </summary>
    Task<Result<ServerRelocationValidation>> ValidateRelocationAsync(
        Guid serverId,
        string newPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a server relocation background job.
    /// </summary>
    /// <param name="serverId">The server to relocate.</param>
    /// <param name="newPath">The new installation path.</param>
    /// <param name="deleteOldFiles">Whether to delete old files after successful copy.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The relocation job ID.</returns>
    Task<Result<Guid>> StartRelocationAsync(
        Guid serverId,
        string newPath,
        bool deleteOldFiles = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current status of a relocation job.
    /// </summary>
    Task<Result<ServerRelocationStatus>> GetRelocationStatusAsync(
        Guid relocationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a running relocation job.
    /// </summary>
    Task<Result> CancelRelocationAsync(
        Guid relocationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the active relocation job for a server, if any.
    /// </summary>
    Task<Result<ServerRelocationStatus?>> GetActiveRelocationForServerAsync(
        Guid serverId,
        CancellationToken cancellationToken = default);
}
