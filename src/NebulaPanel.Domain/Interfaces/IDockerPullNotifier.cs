namespace NebulaPanel.Domain.Interfaces;

/// <summary>
/// Broadcasts Docker image pull progress to connected clients.
/// </summary>
public interface IDockerPullNotifier
{
    Task NotifyPullProgressAsync(Guid serverId, DockerPullProgressInfo progress);
    Task NotifyPullCompleteAsync(Guid serverId, bool success, string? error = null);
}

/// <summary>
/// Aggregated progress information for a Docker image pull operation.
/// </summary>
public record DockerPullProgressInfo(
    string Status,
    string? Layer,
    long BytesDownloaded,
    long TotalBytes,
    double OverallPercent,
    int LayersComplete,
    int LayersTotal);
