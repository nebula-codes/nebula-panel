using Docker.DotNet;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Interfaces;

namespace NebulaPanel.Infrastructure.Services;

/// <summary>
/// Resolves server paths for Docker-in-Docker scenarios by inspecting the panel's own
/// container to discover volume mount mappings. Translates local (container) paths to
/// host paths for game server container bind mounts.
/// </summary>
public class ServerPathResolver : IServerPathResolver
{
    private const string DockerServerBasePath = "/app/servers";
    private const string DockerEnvFile = "/.dockerenv";

    private readonly ILogger<ServerPathResolver> _logger;
    private readonly Dictionary<string, string> _mountMappings = new(StringComparer.Ordinal);

    public bool IsDockerEnvironment { get; }

    public ServerPathResolver(ILogger<ServerPathResolver> logger)
    {
        _logger = logger;
        IsDockerEnvironment = File.Exists(DockerEnvFile);

        if (IsDockerEnvironment)
        {
            _logger.LogInformation("Docker environment detected, initializing path resolver");
            InitializeMountMappings();
        }
        else
        {
            _logger.LogDebug("Native environment detected, path resolver will pass through paths unchanged");
        }
    }

    public string GetServerBasePath()
    {
        if (IsDockerEnvironment)
            return DockerServerBasePath;

        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    public string ResolveHostPath(string localPath)
    {
        if (!IsDockerEnvironment || _mountMappings.Count == 0)
            return localPath;

        // Check for manual override first
        var manualOverride = Environment.GetEnvironmentVariable("Docker__HostServerPath");
        if (!string.IsNullOrEmpty(manualOverride))
        {
            // If the local path starts with our server base path, replace it
            if (localPath.StartsWith(DockerServerBasePath, StringComparison.Ordinal))
            {
                var relativePart = localPath[DockerServerBasePath.Length..];
                var resolved = manualOverride.TrimEnd('/') + relativePart;
                _logger.LogDebug(
                    "Path translated via manual override: {LocalPath} -> {HostPath}",
                    localPath, resolved);
                return resolved;
            }
        }

        // Find the longest matching container prefix
        var bestMatch = string.Empty;
        var bestHostPath = string.Empty;

        foreach (var (containerPath, hostPath) in _mountMappings)
        {
            if (localPath.StartsWith(containerPath, StringComparison.Ordinal) &&
                containerPath.Length > bestMatch.Length)
            {
                // Ensure we match at a path boundary
                if (localPath.Length == containerPath.Length ||
                    localPath[containerPath.Length] == '/')
                {
                    bestMatch = containerPath;
                    bestHostPath = hostPath;
                }
            }
        }

        if (bestMatch.Length > 0)
        {
            var relativePart = localPath[bestMatch.Length..];
            var resolved = bestHostPath.TrimEnd('/') + relativePart;
            _logger.LogDebug(
                "Path translated: {LocalPath} -> {HostPath} (via mount {ContainerPath} -> {MountHostPath})",
                localPath, resolved, bestMatch, bestHostPath);
            return resolved;
        }

        _logger.LogDebug("No mount mapping found for path {LocalPath}, using as-is", localPath);
        return localPath;
    }

    private void InitializeMountMappings()
    {
        try
        {
            var containerId = GetContainerId();
            if (string.IsNullOrEmpty(containerId))
            {
                _logger.LogWarning(
                    "Could not determine container ID. Path translation disabled. " +
                    "Set Docker__HostServerPath env var to override.");
                return;
            }

            _logger.LogInformation("Self-inspecting container {ContainerId} for mount mappings", containerId);

            // Connect to Docker daemon
            var dockerUri = OperatingSystem.IsWindows()
                ? new Uri("npipe://./pipe/docker_engine")
                : new Uri("unix:///var/run/docker.sock");

            using var docker = new DockerClientConfiguration(dockerUri).CreateClient();

            // Inspect our own container
            var inspection = docker.Containers.InspectContainerAsync(containerId)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

            if (inspection.Mounts is null || inspection.Mounts.Count == 0)
            {
                _logger.LogWarning("No mounts found on panel container {ContainerId}", containerId);
                return;
            }

            foreach (var mount in inspection.Mounts)
            {
                if (string.IsNullOrEmpty(mount.Destination) || string.IsNullOrEmpty(mount.Source))
                    continue;

                _mountMappings[mount.Destination] = mount.Source;
                _logger.LogInformation(
                    "Discovered mount mapping: {ContainerPath} -> {HostPath} (Type={Type})",
                    mount.Destination, mount.Source, mount.Type);
            }

            _logger.LogInformation(
                "Path resolver initialized with {Count} mount mapping(s)", _mountMappings.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to inspect panel container for mount mappings. " +
                "Path translation disabled. Set Docker__HostServerPath env var to override.");
        }
    }

    /// <summary>
    /// Determines the current container's ID or name for self-inspection.
    /// Tries multiple methods: HOSTNAME env var, then /proc/self/cgroup parsing.
    /// </summary>
    private string? GetContainerId()
    {
        // Method 1: HOSTNAME env var (Docker sets this to short container ID by default)
        var hostname = Environment.GetEnvironmentVariable("HOSTNAME");
        if (!string.IsNullOrEmpty(hostname))
        {
            _logger.LogDebug("Using HOSTNAME env var as container identifier: {Hostname}", hostname);
            return hostname;
        }

        // Method 2: Parse /proc/self/cgroup for container ID (works on cgroup v1)
        try
        {
            if (File.Exists("/proc/self/cgroup"))
            {
                var cgroupContent = File.ReadAllText("/proc/self/cgroup");
                foreach (var line in cgroupContent.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    // Format: hierarchy-ID:controller-list:cgroup-path
                    // e.g.: 0::/docker/abc123...
                    var parts = line.Split(':');
                    if (parts.Length >= 3)
                    {
                        var path = parts[2];
                        var segments = path.Split('/');
                        var lastSegment = segments[^1];
                        if (lastSegment.Length >= 12)
                        {
                            _logger.LogDebug("Extracted container ID from cgroup: {ContainerId}", lastSegment);
                            return lastSegment;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse /proc/self/cgroup for container ID");
        }

        // Method 3: Parse /proc/self/mountinfo for container ID (works on cgroup v2)
        try
        {
            if (File.Exists("/proc/self/mountinfo"))
            {
                var mountInfo = File.ReadAllText("/proc/self/mountinfo");
                foreach (var line in mountInfo.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    // Look for /docker/ path containing the container ID
                    var dockerIdx = line.IndexOf("/docker/containers/", StringComparison.Ordinal);
                    if (dockerIdx >= 0)
                    {
                        var afterDocker = line[(dockerIdx + "/docker/containers/".Length)..];
                        var slashIdx = afterDocker.IndexOf('/');
                        var id = slashIdx > 0 ? afterDocker[..slashIdx] : afterDocker;
                        if (id.Length >= 12)
                        {
                            _logger.LogDebug("Extracted container ID from mountinfo: {ContainerId}", id);
                            return id;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to parse /proc/self/mountinfo for container ID");
        }

        _logger.LogWarning("Could not determine container ID from any source");
        return null;
    }
}
