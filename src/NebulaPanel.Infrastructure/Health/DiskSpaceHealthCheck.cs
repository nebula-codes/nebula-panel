using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace NebulaPanel.Infrastructure.Health;

/// <summary>
/// Health check that verifies all fixed drives have at least 1GB of free space.
/// Returns Unhealthy if any drive falls below the threshold.
/// </summary>
public class DiskSpaceHealthCheck : IHealthCheck
{
    private readonly ILogger<DiskSpaceHealthCheck> _logger;
    private const long MinimumFreeBytes = 1L * 1024 * 1024 * 1024; // 1 GB

    // Path prefixes to exclude from disk space monitoring (Linux pseudo-filesystems and snap mounts)
    private static readonly string[] ExcludedPathPrefixes =
    [
        "/snap/",      // Snap packages (read-only squashfs)
        "/sys/",       // Sysfs pseudo-filesystem
        "/proc/",      // Procfs pseudo-filesystem
        "/dev/",       // Device filesystem
        "/run/",       // Runtime data
        "/boot/efi",   // EFI partition (typically small by design)
        "/var/lib/nfs/" // NFS pseudo-filesystem (rpc_pipefs)
    ];

    public DiskSpaceHealthCheck(ILogger<DiskSpaceHealthCheck> logger)
    {
        _logger = logger;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>();
        var lowSpaceDrives = new List<string>();

        try
        {
            var drives = DriveInfo.GetDrives()
                .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
                .Where(d => !string.IsNullOrEmpty(d.Name) && !IsExcludedPath(d.Name));

            foreach (var drive in drives)
            {
                var freeGb = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                var totalGb = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                var usagePercent = (1 - (double)drive.AvailableFreeSpace / drive.TotalSize) * 100;

                data[$"{drive.Name.TrimEnd('\\', '/')}_free_gb"] = Math.Round(freeGb, 2);
                data[$"{drive.Name.TrimEnd('\\', '/')}_total_gb"] = Math.Round(totalGb, 2);
                data[$"{drive.Name.TrimEnd('\\', '/')}_usage_percent"] = Math.Round(usagePercent, 1);

                if (drive.AvailableFreeSpace < MinimumFreeBytes)
                {
                    lowSpaceDrives.Add($"{drive.Name} ({freeGb:F2} GB free)");
                }
            }

            if (lowSpaceDrives.Count > 0)
            {
                _logger.LogWarning("Low disk space detected on: {Drives}", string.Join(", ", lowSpaceDrives));
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"Low disk space on: {string.Join(", ", lowSpaceDrives)}",
                    data: data));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                "All drives have sufficient free space (>= 1 GB)",
                data: data));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check disk space");
            return Task.FromResult(HealthCheckResult.Unhealthy(
                $"Failed to check disk space: {ex.Message}",
                exception: ex));
        }
    }

    private static bool IsExcludedPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
            return true; // Exclude invalid paths

        return ExcludedPathPrefixes.Any(prefix =>
            path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
