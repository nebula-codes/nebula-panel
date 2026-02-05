using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Services;

namespace NebulaPanel.Infrastructure.Health;

/// <summary>
/// Health check that monitors system memory usage.
/// Returns Degraded if memory usage exceeds 90%.
/// Uses the existing IHostMonitorService for consistent metrics.
/// </summary>
public class MemoryHealthCheck : IHealthCheck
{
    private readonly IHostMonitorService _hostMonitor;
    private readonly ILogger<MemoryHealthCheck> _logger;
    private const double DegradedThresholdPercent = 90.0;

    public MemoryHealthCheck(
        IHostMonitorService hostMonitor,
        ILogger<MemoryHealthCheck> logger)
    {
        _hostMonitor = hostMonitor;
        _logger = logger;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var health = await _hostMonitor.GetCurrentHealthAsync(cancellationToken).ConfigureAwait(false);

            var data = new Dictionary<string, object>
            {
                ["memory_percent"] = Math.Round(health.MemoryPercent, 1),
                ["memory_used_gb"] = Math.Round(health.MemoryUsedBytes / (1024.0 * 1024.0 * 1024.0), 2),
                ["memory_total_gb"] = Math.Round(health.MemoryTotalBytes / (1024.0 * 1024.0 * 1024.0), 2),
                ["cpu_percent"] = Math.Round(health.CpuPercent, 1)
            };

            if (health.MemoryPercent >= DegradedThresholdPercent)
            {
                _logger.LogWarning(
                    "High memory usage detected: {MemoryPercent:F1}%",
                    health.MemoryPercent);

                return HealthCheckResult.Degraded(
                    $"Memory usage is high: {health.MemoryPercent:F1}%",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                $"Memory usage is normal: {health.MemoryPercent:F1}%",
                data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check memory usage");
            return HealthCheckResult.Unhealthy(
                $"Failed to check memory usage: {ex.Message}",
                exception: ex);
        }
    }
}
