using Docker.DotNet;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace NebulaPanel.Infrastructure.Health;

/// <summary>
/// Health check for Docker daemon connectivity.
/// Returns Healthy if the daemon is reachable, Degraded if unavailable.
/// Docker is optional since native process servers still work without it.
/// </summary>
public class DockerHealthCheck : IHealthCheck, IDisposable
{
    private readonly ILogger<DockerHealthCheck> _logger;
    private readonly DockerClient? _docker;
    private bool _disposed;

    public DockerHealthCheck(ILogger<DockerHealthCheck> logger)
    {
        _logger = logger;

        try
        {
            var dockerUri = GetDockerUri();
            _docker = new DockerClientConfiguration(dockerUri).CreateClient();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create Docker client for health checks");
            _docker = null;
        }
    }

    private static Uri GetDockerUri()
    {
        if (OperatingSystem.IsWindows())
        {
            return new Uri("npipe://./pipe/docker_engine");
        }
        return new Uri("unix:///var/run/docker.sock");
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (_docker is null)
        {
            return HealthCheckResult.Degraded(
                "Docker is not available (optional)",
                data: new Dictionary<string, object> { ["available"] = false });
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var version = await _docker.System.GetVersionAsync(cts.Token).ConfigureAwait(false);

            return HealthCheckResult.Healthy(
                $"Docker daemon is reachable",
                data: new Dictionary<string, object>
                {
                    ["available"] = true,
                    ["version"] = version.Version ?? "unknown",
                    ["apiVersion"] = version.APIVersion ?? "unknown",
                    ["os"] = version.Os ?? "unknown",
                    ["arch"] = version.Arch ?? "unknown"
                });
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Docker health check timed out");
            return HealthCheckResult.Degraded(
                "Docker daemon connection timed out (optional)",
                data: new Dictionary<string, object> { ["available"] = false });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Docker not available");
            return HealthCheckResult.Degraded(
                "Docker is not available (optional)",
                data: new Dictionary<string, object> { ["available"] = false });
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _docker?.Dispose();
        }

        _disposed = true;
    }
}
