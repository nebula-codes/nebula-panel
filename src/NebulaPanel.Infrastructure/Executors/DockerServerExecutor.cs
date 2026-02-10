using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Channels;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.ValueObjects;
using NebulaPanel.Infrastructure.OfficialGames.Minecraft;

namespace NebulaPanel.Infrastructure.Executors;

/// <summary>
/// Executes game servers as Docker containers using Docker.DotNet.
/// Handles container lifecycle, port mappings, volumes, resource limits, and log streaming.
/// </summary>
public class DockerServerExecutor : IServerExecutor, IDisposable
{
    private readonly DockerClient _docker;
    private readonly ILogger<DockerServerExecutor> _logger;
    private readonly IServiceScopeFactory? _serviceScopeFactory;
    private readonly IServerPathResolver _pathResolver;
    private readonly DockerContainerStateStore _containerStore;

    public ServerDeploymentType DeploymentType => ServerDeploymentType.Docker;

    public DockerServerExecutor(
        ILogger<DockerServerExecutor> logger,
        IServerPathResolver pathResolver,
        DockerContainerStateStore containerStore,
        IServiceScopeFactory? serviceScopeFactory = null)
    {
        _logger = logger;
        _pathResolver = pathResolver;
        _containerStore = containerStore;
        _serviceScopeFactory = serviceScopeFactory;

        // Create Docker client based on platform
        var dockerUri = GetDockerUri();
        _logger.LogInformation("Connecting to Docker daemon at {DockerUri}", dockerUri);
        _docker = new DockerClientConfiguration(dockerUri).CreateClient();
    }

    private static Uri GetDockerUri()
    {
        if (OperatingSystem.IsWindows())
        {
            return new Uri("npipe://./pipe/docker_engine");
        }
        return new Uri("unix:///var/run/docker.sock");
    }

    // P/Invoke for Linux UID/GID - allows containers to run as the same user as Nebula Panel
    [DllImport("libc", SetLastError = true)]
    private static extern uint getuid();

    [DllImport("libc", SetLastError = true)]
    private static extern uint getgid();

    /// <summary>
    /// Gets the container user string (UID:GID) for the current process.
    /// This ensures Docker containers run as the same user as Nebula Panel,
    /// preventing permission issues with mounted volumes.
    /// </summary>
    /// <returns>UID:GID string on Linux, null on Windows</returns>
    private static string? GetContainerUser()
    {
        // Windows containers don't use UID:GID
        if (OperatingSystem.IsWindows())
            return null;

        try
        {
            var uid = getuid();
            var gid = getgid();
            return $"{uid}:{gid}";
        }
        catch
        {
            // Fall back to container default on error
            return null;
        }
    }

    public async Task<bool> InstallAsync(
        GameServer server,
        IProgress<InstallProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (server.DockerConfig is null)
        {
            _logger.LogError("Server {ServerId} has no Docker configuration", server.Id);
            return false;
        }

        var config = server.DockerConfig;
        var imageName = $"{config.Image}:{config.Tag}";

        progress?.Report(InstallProgress.Starting());
        _logger.LogInformation("Pulling Docker image {Image} for server {ServerId}", imageName, server.Id);

        try
        {
            // Pull the image
            await _docker.Images.CreateImageAsync(
                new ImagesCreateParameters
                {
                    FromImage = config.Image,
                    Tag = config.Tag
                },
                null,
                new Progress<JSONMessage>(msg =>
                {
                    if (!string.IsNullOrEmpty(msg.Status))
                    {
                        var progressMessage = msg.Progress?.Current ?? 0;
                        var totalBytes = msg.Progress?.Total ?? 0;
                        progress?.Report(InstallProgress.Downloading(
                            msg.Status,
                            progressMessage,
                            totalBytes));
                    }
                }),
                ct).ConfigureAwait(false);

            progress?.Report(InstallProgress.Completed());
            _logger.LogInformation("Successfully pulled image {Image} for server {ServerId}", imageName, server.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to pull Docker image {Image} for server {ServerId}", imageName, server.Id);
            return false;
        }
    }

    public async Task<bool> UpdateAsync(
        GameServer server,
        IProgress<UpdateProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (server.DockerConfig is null)
        {
            _logger.LogError("Server {ServerId} has no Docker configuration", server.Id);
            return false;
        }

        var config = server.DockerConfig;
        var imageName = $"{config.Image}:{config.Tag}";

        progress?.Report(UpdateProgress.CheckingForUpdates());
        _logger.LogInformation("Checking for updates to image {Image} for server {ServerId}", imageName, server.Id);

        try
        {
            // Pull the latest image
            await _docker.Images.CreateImageAsync(
                new ImagesCreateParameters
                {
                    FromImage = config.Image,
                    Tag = config.Tag
                },
                null,
                new Progress<JSONMessage>(msg =>
                {
                    if (!string.IsNullOrEmpty(msg.Status))
                    {
                        var progressMessage = msg.Progress?.Current ?? 0;
                        var totalBytes = msg.Progress?.Total ?? 0;
                        progress?.Report(UpdateProgress.Downloading(
                            msg.Status,
                            progressMessage,
                            totalBytes));
                    }
                }),
                ct).ConfigureAwait(false);

            // If the server was running, we need to recreate the container
            if (!string.IsNullOrEmpty(server.DockerContainerId))
            {
                _logger.LogInformation("Recreating container for server {ServerId} with updated image", server.Id);

                var wasRunning = server.Status == ServerStatus.Running;

                // Stop and remove the old container
                if (wasRunning)
                {
                    await StopAsync(server, force: false, ct).ConfigureAwait(false);
                }

                await RemoveContainerAsync(server, ct).ConfigureAwait(false);

                // Clear the container ID so a new one is created on next start
                server.DockerContainerId = null;

                // Restart if it was running
                if (wasRunning)
                {
                    await StartAsync(server, ct).ConfigureAwait(false);
                }
            }

            progress?.Report(UpdateProgress.Completed());
            _logger.LogInformation("Successfully updated image {Image} for server {ServerId}", imageName, server.Id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update Docker image for server {ServerId}", server.Id);
            return false;
        }
    }

    public async Task<bool> StartAsync(GameServer server, CancellationToken ct = default)
    {
        if (server.DockerConfig is null)
        {
            _logger.LogError("Server {ServerId} has no Docker configuration", server.Id);
            return false;
        }

        var config = server.DockerConfig;
        var effectiveTty = GetEffectiveTty(server, config.Tty);

        try
        {
            // Check if container already exists and is running
            if (!string.IsNullOrEmpty(server.DockerContainerId))
            {
                var existingContainer = await GetContainerInfoAsync(server.DockerContainerId, ct).ConfigureAwait(false);

                if (existingContainer is not null)
                {
                    if (existingContainer.State == "running")
                    {
                        _logger.LogWarning("Container {ContainerId} for server {ServerId} is already running",
                            server.DockerContainerId, server.Id);
                        server.Status = ServerStatus.Running;
                        return true;
                    }

                    // Container exists but is stopped, start it
                    _logger.LogInformation("Starting existing container {ContainerId} for server {ServerId}",
                        server.DockerContainerId, server.Id);

                    server.Status = ServerStatus.Starting;

                    // Clean up any stale managed container from a previous run
                    StopLogStreaming(server.Id);

                    // Attach stdin BEFORE starting to avoid race condition
                    var existingMc = new ManagedContainer(server.Id, server.DockerContainerId, _docker, _logger, effectiveTty);
                    await existingMc.AttachStdinAsync(ct).ConfigureAwait(false);
                    _containerStore.Set(server.Id, existingMc);

                    await _docker.Containers.StartContainerAsync(
                        server.DockerContainerId,
                        new ContainerStartParameters(),
                        ct).ConfigureAwait(false);

                    server.Status = ServerStatus.Running;
                    server.LastStarted = DateTime.UtcNow;

                    // Start log capture after container is running
                    existingMc.StartLogCapture();

                    return true;
                }

                // Container doesn't exist anymore, clear the ID
                _logger.LogWarning("Container {ContainerId} no longer exists, will create new one", server.DockerContainerId);
                server.DockerContainerId = null;
            }

            // Verify Docker daemon is reachable before proceeding
            if (!await IsDockerAvailableAsync(ct).ConfigureAwait(false))
            {
                _logger.LogError(
                    "Docker daemon is not reachable. Ensure the Docker socket is mounted " +
                    "(-v /var/run/docker.sock:/var/run/docker.sock) and the container user " +
                    "has permission to access it.");
                server.Status = ServerStatus.Stopped;
                return false;
            }

            // Create new container
            var containerName = GenerateContainerName(server);

            // Determine the image to use - modpack servers use a Java image
            string imageName;
            string imageRepo;
            string imageTag;

            if (server.InstalledModpack is not null)
            {
                var javaVersion = GetRequiredJavaVersion(server.InstalledModpack.MinecraftVersion ?? "1.21");
                imageRepo = "eclipse-temurin";
                imageTag = $"{javaVersion}-jre";
                imageName = $"{imageRepo}:{imageTag}";
                _logger.LogInformation("Modpack server detected, using Java image: {Image}", imageName);
            }
            else
            {
                imageRepo = config.Image;
                imageTag = config.Tag;
                imageName = $"{imageRepo}:{imageTag}";
            }

            // Check if image exists locally, pull if not
            if (!await ImageExistsAsync(imageName, ct).ConfigureAwait(false))
            {
                _logger.LogInformation("Image {Image} not found locally, pulling...", imageName);
                server.Status = ServerStatus.Installing;

                try
                {
                    await _docker.Images.CreateImageAsync(
                        new ImagesCreateParameters
                        {
                            FromImage = imageRepo,
                            Tag = imageTag
                        },
                        null,
                        new Progress<JSONMessage>(msg =>
                        {
                            if (!string.IsNullOrEmpty(msg.Status))
                            {
                                _logger.LogDebug("[Docker Pull] {Status}", msg.Status);
                            }
                        }),
                        ct).ConfigureAwait(false);

                    _logger.LogInformation("Successfully pulled image {Image}", imageName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to pull image {Image}", imageName);
                    server.Status = ServerStatus.Stopped;
                    return false;
                }
            }

            _logger.LogInformation("Creating container {ContainerName} from image {Image} for server {ServerId}",
                containerName, imageName, server.Id);

            server.Status = ServerStatus.Starting;

            // Ensure any named volumes exist before creating the container
            if (config.Volumes.Count > 0)
            {
                await EnsureNamedVolumesExistAsync(config.Volumes, ct).ConfigureAwait(false);
            }

            // Ensure host paths are writable by the panel user to avoid permission issues
            await EnsureHostPathOwnershipAsync(server, config, ct).ConfigureAwait(false);

            // Ensure bind-mounted file sources exist as files.
            // Docker creates a directory for missing bind mount sources, which causes
            // "not a directory" errors when the container path is a file.
            EnsureBindMountFilesExist(config.Volumes);

            // Get Hytale session tokens if this is a Hytale server
            (string SessionToken, string IdentityToken)? hytaleTokens = null;
            if (IsHytaleServer(server))
            {
                hytaleTokens = await GetHytaleSessionTokensAsync(server, ct).ConfigureAwait(false);
            }

            var createParams = BuildCreateContainerParameters(server, containerName, effectiveTty, hytaleTokens);

            var response = await _docker.Containers.CreateContainerAsync(createParams, ct).ConfigureAwait(false);
            server.DockerContainerId = response.ID;

            _logger.LogInformation("Created container {ContainerId} for server {ServerId}",
                response.ID, server.Id);

            if (response.Warnings?.Count > 0)
            {
                foreach (var warning in response.Warnings)
                {
                    _logger.LogWarning("Container creation warning: {Warning}", warning);
                }
            }

            // IMPORTANT: Attach stdin BEFORE starting the container
            // This ensures the stdin stream is connected to the main process from the start
            var managedContainer = new ManagedContainer(server.Id, response.ID, _docker, _logger, effectiveTty);

            try
            {
                await managedContainer.AttachStdinAsync(ct).ConfigureAwait(false);

                // Only add to managed containers after successful stdin attachment
                _containerStore.Set(server.Id, managedContainer);

                // Now start the container
                await _docker.Containers.StartContainerAsync(response.ID, new ContainerStartParameters(), ct)
                    .ConfigureAwait(false);
            }
            catch
            {
                // Clean up managed container on failure
                managedContainer.Dispose();

                // Clean up created container
                try
                {
                    await _docker.Containers.RemoveContainerAsync(
                        response.ID,
                        new ContainerRemoveParameters { Force = true },
                        CancellationToken.None).ConfigureAwait(false);
                    _logger.LogInformation("Cleaned up container {ContainerId} after start failure", response.ID);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Failed to clean up container {ContainerId} after start failure", response.ID);
                }

                // Clear the container ID since we removed it
                server.DockerContainerId = null;

                throw;
            }

            server.Status = ServerStatus.Running;
            server.LastStarted = DateTime.UtcNow;

            // Start log streaming (separate from stdin)
            managedContainer.StartLogCapture();

            // Start exit code monitoring for Hytale servers (handles exit code 8 for update restart)
            if (IsHytaleServer(server))
            {
                _ = MonitorContainerExitAsync(server, response.ID);
            }

            _logger.LogInformation("Started container {ContainerId} for server {ServerId}",
                response.ID, server.Id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Docker container for server {ServerId}", server.Id);
            server.Status = ServerStatus.Crashed;
            return false;
        }
    }

    public async Task<bool> StopAsync(GameServer server, bool force = false, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(server.DockerContainerId))
        {
            _logger.LogWarning("No container ID for server {ServerId}", server.Id);
            server.Status = ServerStatus.Stopped;
            return true;
        }

        try
        {
            server.Status = ServerStatus.Stopping;

            // Stop log streaming
            StopLogStreaming(server.Id);

            if (force)
            {
                _logger.LogInformation("Force killing container {ContainerId} for server {ServerId}",
                    server.DockerContainerId, server.Id);

                await _docker.Containers.KillContainerAsync(
                    server.DockerContainerId,
                    new ContainerKillParameters(),
                    ct).ConfigureAwait(false);
            }
            else
            {
                _logger.LogInformation("Stopping container {ContainerId} for server {ServerId} with 30s timeout",
                    server.DockerContainerId, server.Id);

                await _docker.Containers.StopContainerAsync(
                    server.DockerContainerId,
                    new ContainerStopParameters { WaitBeforeKillSeconds = 30 },
                    ct).ConfigureAwait(false);
            }

            server.Status = ServerStatus.Stopped;
            server.LastStopped = DateTime.UtcNow;

            _logger.LogInformation("Stopped container {ContainerId} for server {ServerId}",
                server.DockerContainerId, server.Id);

            // End Hytale game session if applicable
            if (IsHytaleServer(server))
            {
                await EndHytaleSessionAsync(server, ct).ConfigureAwait(false);
            }

            return true;
        }
        catch (DockerContainerNotFoundException)
        {
            _logger.LogWarning("Container {ContainerId} not found for server {ServerId}",
                server.DockerContainerId, server.Id);
            server.DockerContainerId = null;
            server.Status = ServerStatus.Stopped;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop container for server {ServerId}", server.Id);
            return false;
        }
    }

    public async Task<bool> RestartAsync(GameServer server, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(server.DockerContainerId))
        {
            _logger.LogWarning("No container ID for server {ServerId}, starting new container", server.Id);
            return await StartAsync(server, ct).ConfigureAwait(false);
        }

        try
        {
            _logger.LogInformation("Restarting container {ContainerId} for server {ServerId}",
                server.DockerContainerId, server.Id);

            server.Status = ServerStatus.Stopping;

            // Stop log streaming before restart
            StopLogStreaming(server.Id);

            await _docker.Containers.RestartContainerAsync(
                server.DockerContainerId,
                new ContainerRestartParameters { WaitBeforeKillSeconds = 30 },
                ct).ConfigureAwait(false);

            server.Status = ServerStatus.Running;
            server.LastStarted = DateTime.UtcNow;

            // Reattach stdin and start log capture (RestartContainerAsync waits for running state)
            var tty = GetEffectiveTty(server, server.DockerConfig?.Tty ?? false);
            var managedContainer = new ManagedContainer(server.Id, server.DockerContainerId!, _docker, _logger, tty);
            await managedContainer.AttachStdinAsync(ct).ConfigureAwait(false);
            _containerStore.Set(server.Id, managedContainer);
            managedContainer.StartLogCapture();

            _logger.LogInformation("Restarted container {ContainerId} for server {ServerId}",
                server.DockerContainerId, server.Id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart container for server {ServerId}", server.Id);
            server.Status = ServerStatus.Crashed;
            return false;
        }
    }

    public async Task<ServerStatus> GetStatusAsync(GameServer server, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(server.DockerContainerId))
        {
            return ServerStatus.Stopped;
        }

        try
        {
            var container = await GetContainerInfoAsync(server.DockerContainerId, ct).ConfigureAwait(false);

            if (container is null)
            {
                return ServerStatus.Stopped;
            }

            return container.State.ToLowerInvariant() switch
            {
                "running" => ServerStatus.Running,
                "created" => ServerStatus.Stopped,
                "restarting" => ServerStatus.Starting,
                "removing" => ServerStatus.Stopping,
                "paused" => ServerStatus.Stopped,
                "exited" => ServerStatus.Stopped,
                "dead" => ServerStatus.Crashed,
                _ => ServerStatus.Unknown
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get status for container {ContainerId}", server.DockerContainerId);
            return ServerStatus.Unknown;
        }
    }

    public async IAsyncEnumerable<string> StreamConsoleAsync(
        GameServer server,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(server.DockerContainerId))
        {
            _logger.LogWarning("No container ID for server {ServerId}", server.Id);
            yield break;
        }

        // Check if we have a managed container with buffered logs
        var managedContainer = _containerStore.Get(server.Id);
        if (managedContainer is not null)
        {
            await foreach (var line in managedContainer.ReadOutputAsync(ct).ConfigureAwait(false))
            {
                yield return line;
            }
            yield break;
        }

        // Fall back to direct log streaming
        var logParams = new ContainerLogsParameters
        {
            ShowStdout = true,
            ShowStderr = true,
            Follow = true,
            Tail = "100",
            Timestamps = true
        };

        MultiplexedStream? stream = null;
        try
        {
            stream = await _docker.Containers.GetContainerLogsAsync(
                server.DockerContainerId,
                false, // non-TTY mode
                logParams,
                ct).ConfigureAwait(false);

            await foreach (var line in ParseDockerLogStreamAsync(stream, ct).ConfigureAwait(false))
            {
                yield return line;
            }
        }
        finally
        {
            stream?.Dispose();
        }
    }

    public async Task SendCommandAsync(GameServer server, string command, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(server.DockerContainerId))
        {
            throw new InvalidOperationException($"Server {server.Id} has no container");
        }

        try
        {
            // If the container uses tmux-based command injection (e.g. jacobsmile/tmodloader1.4),
            // send via `docker exec <container> inject "command"` instead of stdin.
            if (server.DockerConfig?.CommandMode == DockerCommandMode.TmuxInject)
            {
                _logger.LogDebug("Sending command via tmux inject to container {ContainerId}", server.DockerContainerId);
                await SendCommandViaTmuxInjectAsync(server.DockerContainerId, command, ct).ConfigureAwait(false);
                _logger.LogInformation("Command sent via tmux inject to container {ContainerId}: {Command}",
                    server.DockerContainerId, command);
                return;
            }

            // Check if we have a managed container
            _logger.LogInformation("Looking for managed container for server {ServerId}, managed server IDs count: {Count}",
                server.Id, _containerStore.GetAllServerIds().Count);

            var managedContainer = _containerStore.Get(server.Id);
            if (managedContainer is not null)
            {
                // Use the attached stdin stream - this goes through Docker's attach mechanism
                // which properly handles TTY mode (like docker attach from CLI)
                if (managedContainer.HasStdinStream)
                {
                    _logger.LogDebug("Using managed stdin for container {ContainerId} (TTY={Tty})", server.DockerContainerId, managedContainer.IsTty);
                    _logger.LogInformation("Sending command via attached stdin stream to container {ContainerId}",
                        server.DockerContainerId);
                    await managedContainer.SendCommandAsync(command, ct).ConfigureAwait(false);
                    return;
                }
            }

            // Fallback: ensure we have a managed container with an attached stdin stream.
            _logger.LogInformation("No managed container found for server {ServerId}, attaching stdin", server.Id);

            var containerInfo = await _docker.Containers.InspectContainerAsync(server.DockerContainerId, ct).ConfigureAwait(false);

            _logger.LogInformation("Container {ContainerId} config: AttachStdin={AttachStdin}, OpenStdin={OpenStdin}, Tty={Tty}",
                server.DockerContainerId, containerInfo.Config.AttachStdin, containerInfo.Config.OpenStdin, containerInfo.Config.Tty);

            managedContainer ??= new ManagedContainer(server.Id, server.DockerContainerId, _docker, _logger, containerInfo.Config.Tty);
            _containerStore.Set(server.Id, managedContainer);
            await managedContainer.AttachStdinAsync(ct).ConfigureAwait(false);

            if (managedContainer.HasStdinStream)
            {
                _logger.LogDebug("Attached stdin for container {ContainerId}; using managed stdin (TTY={Tty})",
                    server.DockerContainerId, managedContainer.IsTty);
                _logger.LogInformation("Sending command via attached stdin stream to container {ContainerId}",
                    server.DockerContainerId);
                await managedContainer.SendCommandAsync(command, ct).ConfigureAwait(false);
            }
            // For TTY containers or failed stdin attach, use exec method as fallback
            else if (containerInfo.Config.Tty)
            {
                _logger.LogDebug("Managed stdin unavailable for TTY container {ContainerId}; falling back to exec",
                    server.DockerContainerId);
                _logger.LogInformation("TTY container detected, using exec method for {ContainerId}", server.DockerContainerId);
                await SendCommandViaExecAsync(server.DockerContainerId, command, true, ct).ConfigureAwait(false);
            }
            else
            {
                _logger.LogDebug("Managed stdin unavailable for non-TTY container {ContainerId}; falling back to exec",
                    server.DockerContainerId);
                // Use exec as last resort
                _logger.LogWarning("Container {ContainerId} doesn't support stdin attach, using exec fallback",
                    server.DockerContainerId);
                await SendCommandViaExecAsync(server.DockerContainerId, command, false, ct).ConfigureAwait(false);
            }

            _logger.LogInformation("Command sent successfully to container {ContainerId}: {Command}",
                server.DockerContainerId, command);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send command to container {ContainerId}", server.DockerContainerId);
            throw;
        }
    }

    public async Task<ResourceUsage> GetResourceUsageAsync(GameServer server, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(server.DockerContainerId))
        {
            return new ResourceUsage();
        }

        try
        {
            var stats = await GetContainerStatsAsync(server.DockerContainerId, ct).ConfigureAwait(false);

            if (stats is null)
            {
                return new ResourceUsage();
            }

            // Calculate CPU percentage
            var cpuDelta = stats.CPUStats.CPUUsage.TotalUsage - stats.PreCPUStats.CPUUsage.TotalUsage;
            var systemDelta = stats.CPUStats.SystemUsage - stats.PreCPUStats.SystemUsage;
            var cpuPercent = 0.0;

            if (systemDelta > 0 && cpuDelta > 0)
            {
                var cpuCount = (double)stats.CPUStats.OnlineCPUs;
                if (cpuCount == 0)
                {
                    cpuCount = stats.CPUStats.CPUUsage.PercpuUsage?.Count ?? 1;
                }
                cpuPercent = ((double)cpuDelta / systemDelta) * cpuCount * 100.0;
            }

            // Get container info for uptime
            var containerInfo = await _docker.Containers.InspectContainerAsync(server.DockerContainerId, ct).ConfigureAwait(false);
            var uptime = TimeSpan.Zero;

            if (DateTime.TryParse(containerInfo.State.StartedAt, out var startTime))
            {
                uptime = DateTime.UtcNow - startTime.ToUniversalTime();
            }

            return new ResourceUsage
            {
                CpuPercent = Math.Round(cpuPercent, 2),
                MemoryBytes = (long)stats.MemoryStats.Usage,
                VirtualMemoryBytes = (long)(stats.MemoryStats.Stats?.TryGetValue("total_rss", out var rss) == true ? rss : 0),
                ThreadCount = 0, // Not directly available from Docker stats
                HandleCount = 0,
                Uptime = uptime,
                Timestamp = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get resource usage for container {ContainerId}", server.DockerContainerId);
            return new ResourceUsage();
        }
    }

    /// <summary>
    /// Removes a container. Call after stopping if you want to clean up.
    /// </summary>
    public Task CleanupAsync(GameServer server, CancellationToken ct = default)
        => RemoveContainerAsync(server, ct);

    public async Task RemoveContainerAsync(GameServer server, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(server.DockerContainerId))
        {
            return;
        }

        try
        {
            _logger.LogInformation("Removing container {ContainerId} for server {ServerId}",
                server.DockerContainerId, server.Id);

            await _docker.Containers.RemoveContainerAsync(
                server.DockerContainerId,
                new ContainerRemoveParameters { Force = true, RemoveVolumes = false },
                ct).ConfigureAwait(false);

            _containerStore.TryRemove(server.Id, out var removedContainer);
            removedContainer?.Dispose();

            server.DockerContainerId = null;

            _logger.LogInformation("Removed container for server {ServerId}", server.Id);
        }
        catch (DockerContainerNotFoundException)
        {
            _logger.LogWarning("Container {ContainerId} already removed", server.DockerContainerId);
            server.DockerContainerId = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove container {ContainerId}", server.DockerContainerId);
            throw;
        }
    }

    private CreateContainerParameters BuildCreateContainerParameters(
        GameServer server,
        string containerName,
        bool effectiveTty,
        (string SessionToken, string IdentityToken)? hytaleTokens = null)
    {
        var config = server.DockerConfig!;

        // Check if this is a modpack server that needs special handling
        if (server.InstalledModpack is not null)
        {
            return BuildModpackContainerParameters(server, containerName);
        }

        var imageName = $"{config.Image}:{config.Tag}";

        var createParams = new CreateContainerParameters
        {
            Image = imageName,
            Name = containerName,
            Env = BuildEnvironmentVariables(config),
            AttachStdin = true,
            AttachStdout = true,
            AttachStderr = true,
            OpenStdin = true,
            StdinOnce = false,  // Keep stdin open for multiple commands
            Tty = effectiveTty,   // Some servers (like Hytale) need TTY for stdin command processing
            HostConfig = new HostConfig
            {
                PortBindings = BuildPortBindings(config.Ports, server, includeServerPorts: true),
                Binds = BuildVolumeMountsWithSystemVolumes(config.Volumes),
                RestartPolicy = BuildRestartPolicy(config.RestartPolicy),
                NetworkMode = config.Network ?? "bridge"
            }
        };

        // Set container user to match host user (Linux only)
        // This prevents permission issues with bind-mounted volumes
        // Skip if RunAsRoot is enabled (some images like tModLoader require root)
        if (!config.RunAsRoot)
        {
            var containerUser = GetContainerUser();
            if (!string.IsNullOrEmpty(containerUser))
            {
                createParams.User = containerUser;
                _logger.LogDebug("Container will run as user {User}", containerUser);
            }
        }
        else
        {
            _logger.LogDebug("Container will run as root/default user (RunAsRoot enabled)");
        }

        // Apply working directory if specified
        if (!string.IsNullOrEmpty(config.WorkingDirectory))
        {
            createParams.WorkingDir = config.WorkingDirectory;
        }

        // Apply custom command if specified
        if (!string.IsNullOrEmpty(config.Command))
        {
            // Split command into parts, respecting quoted strings
            var cmdParts = SplitCommand(config.Command).ToList();

            // Runtime AOT cache detection for Hytale servers
            // The AOT cache may be downloaded with updates after initial installation
            if (IsHytaleServer(server) && !cmdParts.Any(p => p.Contains("-XX:AOTCache")))
            {
                var aotCachePath = Path.Combine(server.InstallPath, "Server", "HytaleServer.aot");
                if (File.Exists(aotCachePath))
                {
                    // Find -jar index and insert AOT arg before it
                    var jarIndex = cmdParts.FindIndex(p => p == "-jar");
                    if (jarIndex > 0)
                    {
                        cmdParts.Insert(jarIndex, "-XX:AOTCache=HytaleServer.aot");
                        _logger.LogInformation(
                            "Detected AOT cache for Hytale server {ServerId}, injecting -XX:AOTCache argument",
                            server.Id);
                    }
                }
            }

            // Append Hytale session tokens as CLI arguments if available
            if (hytaleTokens.HasValue && IsHytaleServer(server))
            {
                cmdParts.Add("--session-token");
                cmdParts.Add(hytaleTokens.Value.SessionToken);
                cmdParts.Add("--identity-token");
                cmdParts.Add(hytaleTokens.Value.IdentityToken);
                _logger.LogInformation("Appended Hytale session tokens to container command for server {ServerId}", server.Id);
            }

            createParams.Cmd = cmdParts;
        }
        else if (hytaleTokens.HasValue && IsHytaleServer(server))
        {
            // If no custom command but we have Hytale tokens, we need to get the default command
            // and append tokens to it. This case shouldn't happen normally since Hytale servers
            // should have a command configured, but handle it gracefully.
            _logger.LogWarning("Hytale server {ServerId} has no command configured but has tokens - tokens may not be passed", server.Id);
        }

        // Apply custom entrypoint if specified
        if (!string.IsNullOrEmpty(config.Entrypoint))
        {
            createParams.Entrypoint = SplitCommand(config.Entrypoint);
        }

        // Apply resource limits
        ApplyResourceLimits(createParams.HostConfig, config.Limits ?? server.ResourceLimits);

        return createParams;
    }

    private bool GetEffectiveTty(GameServer server, bool configuredTty)
    {
        if (configuredTty &&
            OperatingSystem.IsWindows() &&
            IsHytaleServer(server))
        {
            _logger.LogInformation("Overriding TTY to false for Hytale server {ServerId} on Windows", server.Id);
            return false;
        }

        return configuredTty;
    }

    private static bool IsHytaleServer(GameServer server)
    {
        if (server.HytaleInfo is not null)
        {
            return true;
        }

        var gameName = server.Game?.Name;
        return !string.IsNullOrEmpty(gameName) &&
               gameName.Equals("Hytale", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Monitors a container for exit and handles exit code 8 (Hytale update restart).
    /// </summary>
    private async Task MonitorContainerExitAsync(GameServer server, string containerId)
    {
        try
        {
            // Wait for container to exit
            var waitResponse = await _docker.Containers.WaitContainerAsync(containerId).ConfigureAwait(false);

            _logger.LogInformation(
                "Container {ContainerId} for server {ServerId} exited with code {ExitCode}",
                containerId, server.Id, waitResponse.StatusCode);

            // Exit code 8 = Hytale update restart request (from official start scripts)
            if (waitResponse.StatusCode == 8)
            {
                _logger.LogInformation(
                    "Server {ServerId} requested update restart (exit code 8), restarting automatically",
                    server.Id);

                server.Status = ServerStatus.Restarting;

                // Clean up old container
                StopLogStreaming(server.Id);

                // Brief delay before restart
                await Task.Delay(2000).ConfigureAwait(false);

                // Remove old container and start fresh
                await RemoveContainerAsync(server, CancellationToken.None).ConfigureAwait(false);

                // Restart the server
                var success = await StartAsync(server, CancellationToken.None).ConfigureAwait(false);
                if (!success)
                {
                    _logger.LogError("Failed to restart server {ServerId} after exit code 8", server.Id);
                    server.Status = ServerStatus.Crashed;
                }
            }
            else
            {
                // Normal exit handling
                server.Status = waitResponse.StatusCode == 0 ? ServerStatus.Stopped : ServerStatus.Crashed;
                server.LastStopped = DateTime.UtcNow;
            }
        }
        catch (DockerContainerNotFoundException)
        {
            // Container was removed externally
            _logger.LogDebug("Container {ContainerId} was removed, stopping exit monitor", containerId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error monitoring container exit for server {ServerId}", server.Id);
        }
    }

    /// <summary>
    /// Gets Hytale session tokens for the server owner.
    /// Returns (sessionToken, identityToken) or null if not available.
    /// </summary>
    private async Task<(string SessionToken, string IdentityToken)?> GetHytaleSessionTokensAsync(
        GameServer server,
        CancellationToken ct)
    {
        if (_serviceScopeFactory is null)
        {
            _logger.LogWarning("Cannot get Hytale session tokens: IServiceScopeFactory not available");
            return null;
        }

        if (server.OwnerId == Guid.Empty)
        {
            _logger.LogWarning("Cannot get Hytale session tokens: server {ServerId} has no owner", server.Id);
            return null;
        }

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var sessionManager = scope.ServiceProvider.GetRequiredService<IHytaleSessionManager>();

            var credentials = await sessionManager.EnsureSessionAsync(server.OwnerId, ct).ConfigureAwait(false);
            if (credentials?.SessionToken is null || credentials.IdentityToken is null)
            {
                _logger.LogWarning(
                    "No valid Hytale session for user {UserId}, server will start without auth tokens",
                    server.OwnerId);
                return null;
            }

            _logger.LogInformation(
                "Retrieved Hytale session tokens for server {ServerId} (expires {ExpiresAt})",
                server.Id, credentials.SessionExpiresAt);

            return (credentials.SessionToken, credentials.IdentityToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Hytale session tokens for server {ServerId}", server.Id);
            return null;
        }
    }

    /// <summary>
    /// Ends the Hytale game session for the server owner.
    /// </summary>
    private async Task EndHytaleSessionAsync(GameServer server, CancellationToken ct)
    {
        if (_serviceScopeFactory is null || server.OwnerId == Guid.Empty)
        {
            return;
        }

        try
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var sessionManager = scope.ServiceProvider.GetRequiredService<IHytaleSessionManager>();
            await sessionManager.EndSessionAsync(server.OwnerId, ct).ConfigureAwait(false);
            _logger.LogInformation("Ended Hytale game session for server {ServerId}", server.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to end Hytale session for server {ServerId}", server.Id);
        }
    }

    /// <summary>
    /// Splits a command string into parts, respecting quoted strings.
    /// </summary>
    private static IList<string> SplitCommand(string command)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var quoteChar = '\0';

        foreach (var c in command)
        {
            if (!inQuotes && (c == '"' || c == '\''))
            {
                inQuotes = true;
                quoteChar = c;
            }
            else if (inQuotes && c == quoteChar)
            {
                inQuotes = false;
                quoteChar = '\0';
            }
            else if (!inQuotes && c == ' ')
            {
                if (current.Length > 0)
                {
                    parts.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            parts.Add(current.ToString());
        }

        return parts;
    }

    /// <summary>
    /// Builds container parameters specifically for modpack servers.
    /// Uses a Java runtime image and mounts the pre-installed modpack files.
    /// </summary>
    private CreateContainerParameters BuildModpackContainerParameters(GameServer server, string containerName)
    {
        var config = server.DockerConfig!;
        var modpack = server.InstalledModpack!;

        // Determine Java version based on Minecraft version
        var javaVersion = GetRequiredJavaVersion(modpack.MinecraftVersion ?? "1.21");
        var javaImage = $"eclipse-temurin:{javaVersion}-jre";

        _logger.LogInformation(
            "Building modpack container for {ServerName} with {Loader} on MC {McVersion} using Java {JavaVersion}",
            server.Name, modpack.LoaderType, modpack.MinecraftVersion, javaVersion);

        // Build the startup command
        var startupCommand = BuildModpackStartupCommand(server, modpack);

        _logger.LogInformation("Modpack startup command: {Command}", string.Join(" ", startupCommand));

        // Build environment variables
        var envVars = new List<string>
        {
            "EULA=TRUE"
        };

        // Add JVM memory settings if resource limits are configured
        var memoryMb = config.Limits?.MaxMemoryMb ?? server.ResourceLimits?.MaxMemoryMb ?? 4096;
        var heapSize = (int)(memoryMb * 0.75); // Use 75% of container memory for heap
        envVars.Add($"JVM_OPTS=-Xmx{heapSize}M -Xms{heapSize / 2}M");

        // Add any custom environment variables from config
        foreach (var kvp in config.EnvironmentVariables)
        {
            envVars.Add($"{kvp.Key}={kvp.Value}");
        }

        // Build volume mounts - always mount the server directory
        // Translate the local path to host path for Docker bind mounts
        var hostInstallPath = _pathResolver.ResolveHostPath(server.InstallPath);
        if (hostInstallPath != server.InstallPath)
        {
            _logger.LogInformation(
                "Translated install path for Docker bind mount: {LocalPath} -> {HostPath}",
                server.InstallPath, hostInstallPath);
        }
        var binds = new List<string>
        {
            $"{hostInstallPath}:/data:rw"
        };

        // Add any additional volume mounts from config
        binds.AddRange(BuildVolumeMountsWithSystemVolumes(config.Volumes));

        var createParams = new CreateContainerParameters
        {
            Image = javaImage,
            Name = containerName,
            Env = envVars,
            WorkingDir = "/data",
            Cmd = startupCommand,
            AttachStdin = true,
            AttachStdout = true,
            AttachStderr = true,
            OpenStdin = true,
            StdinOnce = false,  // Keep stdin open for multiple commands
            Tty = false,  // Disabled - use RCON for commands instead of stdin
            HostConfig = new HostConfig
            {
                PortBindings = BuildPortBindings(config.Ports, server, includeServerPorts: true),
                Binds = binds,
                RestartPolicy = BuildRestartPolicy(config.RestartPolicy),
                NetworkMode = config.Network ?? "bridge"
            }
        };

        // Set container user to match host user (Linux only)
        // This prevents permission issues with bind-mounted volumes
        var containerUser = GetContainerUser();
        if (!string.IsNullOrEmpty(containerUser))
        {
            createParams.User = containerUser;
            _logger.LogDebug("Modpack container will run as user {User}", containerUser);
        }

        // Apply resource limits
        ApplyResourceLimits(createParams.HostConfig, config.Limits ?? server.ResourceLimits);

        return createParams;
    }

    /// <summary>
    /// Builds the startup command for a modpack server based on loader type.
    /// </summary>
    private IList<string> BuildModpackStartupCommand(GameServer server, InstalledModpackInfo modpack)
    {
        // Priority 1: Check if user has configured a specific startup script in NativeConfig
        if (server.NativeConfig?.UseStartupScript == true && !string.IsNullOrEmpty(server.NativeConfig.StartupScriptPath))
        {
            var configuredScriptPath = Path.Combine(server.InstallPath, server.NativeConfig.StartupScriptPath);
            if (File.Exists(configuredScriptPath))
            {
                _logger.LogInformation("Using user-configured startup script: {Script}", server.NativeConfig.StartupScriptPath);

                // Always try to parse the script to extract the Java command
                // This ensures Java runs as PID 1 and receives stdin properly
                var parsed = StartupScriptDetector.ParseStartupScript(configuredScriptPath);
                if (parsed != null)
                {
                    var dockerCommand = StartupScriptDetector.BuildDockerCommand(parsed);
                    _logger.LogInformation("Built Docker command from configured script: {Command}", string.Join(" ", dockerCommand));
                    return dockerCommand;
                }

                // Fallback: For .sh files that couldn't be parsed, run with exec to replace shell with java
                if (configuredScriptPath.EndsWith(".sh", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Could not parse startup script, running via shell (stdin may not work): {Script}", server.NativeConfig.StartupScriptPath);
                    return ["sh", "-c", $"exec sh {server.NativeConfig.StartupScriptPath}"];
                }
            }
            else
            {
                _logger.LogWarning("Configured startup script not found: {Script}", configuredScriptPath);
            }
        }

        // Priority 2: Auto-detect startup scripts
        var allScripts = StartupScriptDetector.DetectAllScripts(server.InstallPath);

        foreach (var script in allScripts)
        {
            var scriptPath = Path.Combine(server.InstallPath, script);
            var parsed = StartupScriptDetector.ParseStartupScript(scriptPath);

            if (parsed != null)
            {
                _logger.LogInformation(
                    "Parsed detected startup script {Script}: JvmArgs={ArgCount}, JarFile={Jar}, UsesArgsFile={UsesArgs}",
                    script, parsed.JvmArgs.Count, parsed.JarFile, parsed.UsesArgsFile);

                var dockerCommand = StartupScriptDetector.BuildDockerCommand(parsed);
                _logger.LogInformation("Built Docker command from detected script: {Command}", string.Join(" ", dockerCommand));
                return dockerCommand;
            }
        }

        // Priority 3: Fall back to loader-specific startup
        _logger.LogInformation("No startup script found, using loader-specific detection for {Loader}", modpack.LoaderType);
        return modpack.LoaderType switch
        {
            ModLoaderType.Forge => BuildForgeStartupCommand(server, modpack),
            ModLoaderType.NeoForge => BuildNeoForgeStartupCommand(server, modpack),
            ModLoaderType.Fabric => BuildFabricStartupCommand(server, modpack),
            ModLoaderType.Quilt => BuildQuiltStartupCommand(server, modpack),
            _ => BuildVanillaStartupCommand(server, modpack)
        };
    }

    /// <summary>
    /// Builds startup command for Forge servers (modern 1.17+).
    /// </summary>
    private IList<string> BuildForgeStartupCommand(GameServer server, InstalledModpackInfo modpack)
    {
        var mcVersion = modpack.MinecraftVersion ?? "1.20.1";
        var isModern = IsModernForge(mcVersion);

        if (isModern)
        {
            // Modern Forge uses @args files
            var argsFile = FindForgeArgsFile(server.InstallPath, mcVersion);
            if (argsFile != null)
            {
                // Need to run java with the args file
                return ["java", $"@user_jvm_args.txt", $"@{argsFile}", "nogui"];
            }
        }

        // Legacy or fallback: find the forge jar
        var forgeJar = FindServerJar(server.InstallPath, "forge");
        if (forgeJar != null)
        {
            return ["java", "-jar", forgeJar, "nogui"];
        }

        // Last resort: look for any server jar
        return BuildVanillaStartupCommand(server, modpack);
    }

    /// <summary>
    /// Builds startup command for NeoForge servers.
    /// </summary>
    private IList<string> BuildNeoForgeStartupCommand(GameServer server, InstalledModpackInfo modpack)
    {
        // NeoForge also uses @args files like modern Forge
        var argsFile = FindNeoForgeArgsFile(server.InstallPath);
        if (argsFile != null)
        {
            return ["java", $"@user_jvm_args.txt", $"@{argsFile}", "nogui"];
        }

        // Fallback to jar file
        var neoforgeJar = FindServerJar(server.InstallPath, "neoforge");
        if (neoforgeJar != null)
        {
            return ["java", "-jar", neoforgeJar, "nogui"];
        }

        return BuildVanillaStartupCommand(server, modpack);
    }

    /// <summary>
    /// Builds startup command for Fabric servers.
    /// </summary>
    private IList<string> BuildFabricStartupCommand(GameServer server, InstalledModpackInfo modpack)
    {
        // Fabric uses fabric-server-launch.jar or fabric-server-*.jar
        var fabricJar = FindServerJar(server.InstallPath, "fabric");
        if (fabricJar != null)
        {
            return ["java", "-jar", fabricJar, "nogui"];
        }

        return BuildVanillaStartupCommand(server, modpack);
    }

    /// <summary>
    /// Builds startup command for Quilt servers.
    /// </summary>
    private IList<string> BuildQuiltStartupCommand(GameServer server, InstalledModpackInfo modpack)
    {
        // Quilt uses quilt-server-launch.jar
        var quiltJar = FindServerJar(server.InstallPath, "quilt");
        if (quiltJar != null)
        {
            return ["java", "-jar", quiltJar, "nogui"];
        }

        return BuildVanillaStartupCommand(server, modpack);
    }

    /// <summary>
    /// Builds startup command for vanilla or unknown servers.
    /// </summary>
    private IList<string> BuildVanillaStartupCommand(GameServer server, InstalledModpackInfo modpack)
    {
        // Look for common server jar names
        var serverJar = FindServerJar(server.InstallPath, "server")
                        ?? FindServerJar(server.InstallPath, "minecraft")
                        ?? FindAnyServerJar(server.InstallPath);

        if (serverJar != null)
        {
            return ["java", "-jar", serverJar, "nogui"];
        }

        _logger.LogWarning("Could not find server jar in {Path}, using default", server.InstallPath);
        return ["java", "-jar", "server.jar", "nogui"];
    }

    /// <summary>
    /// Finds a server jar file matching the given pattern.
    /// </summary>
    private static string? FindServerJar(string serverPath, string pattern)
    {
        if (!Directory.Exists(serverPath))
            return null;

        var jars = Directory.GetFiles(serverPath, $"*{pattern}*.jar", SearchOption.TopDirectoryOnly)
            .Where(f => !Path.GetFileName(f).Contains("installer", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(f => new FileInfo(f).Length)
            .ToList();

        return jars.Count > 0 ? Path.GetFileName(jars[0]) : null;
    }

    /// <summary>
    /// Finds any jar file that looks like a server jar.
    /// </summary>
    private static string? FindAnyServerJar(string serverPath)
    {
        if (!Directory.Exists(serverPath))
            return null;

        // Prioritize common names
        var priorityNames = new[] { "server.jar", "minecraft_server.jar" };
        foreach (var name in priorityNames)
        {
            if (File.Exists(Path.Combine(serverPath, name)))
                return name;
        }

        // Look for any jar that might be a server
        var jars = Directory.GetFiles(serverPath, "*.jar", SearchOption.TopDirectoryOnly)
            .Where(f =>
            {
                var name = Path.GetFileName(f).ToLowerInvariant();
                return !name.Contains("installer") &&
                       !name.Contains("client") &&
                       !name.Contains("api") &&
                       !name.Contains("library");
            })
            .OrderByDescending(f => new FileInfo(f).Length)
            .ToList();

        return jars.Count > 0 ? Path.GetFileName(jars[0]) : null;
    }

    /// <summary>
    /// Finds the Forge args file for modern Forge installations.
    /// </summary>
    private static string? FindForgeArgsFile(string serverPath, string mcVersion)
    {
        var argsFileName = OperatingSystem.IsWindows() ? "win_args.txt" : "unix_args.txt";
        var forgeLibDir = Path.Combine(serverPath, "libraries", "net", "minecraftforge", "forge");

        if (!Directory.Exists(forgeLibDir))
            return null;

        var forgeDirs = Directory.GetDirectories(forgeLibDir, $"{mcVersion}-*");
        foreach (var dir in forgeDirs.OrderByDescending(d => new DirectoryInfo(d).CreationTime))
        {
            var argsFile = Path.Combine(dir, argsFileName);
            if (File.Exists(argsFile))
            {
                var dirName = Path.GetFileName(dir);
                return $"libraries/net/minecraftforge/forge/{dirName}/{argsFileName}";
            }
        }

        return null;
    }

    /// <summary>
    /// Finds the NeoForge args file.
    /// </summary>
    private static string? FindNeoForgeArgsFile(string serverPath)
    {
        var argsFileName = OperatingSystem.IsWindows() ? "win_args.txt" : "unix_args.txt";
        var neoforgeLibDir = Path.Combine(serverPath, "libraries", "net", "neoforged", "neoforge");

        if (!Directory.Exists(neoforgeLibDir))
            return null;

        var neoforgeDirs = Directory.GetDirectories(neoforgeLibDir);
        foreach (var dir in neoforgeDirs.OrderByDescending(d => new DirectoryInfo(d).CreationTime))
        {
            var argsFile = Path.Combine(dir, argsFileName);
            if (File.Exists(argsFile))
            {
                var dirName = Path.GetFileName(dir);
                return $"libraries/net/neoforged/neoforge/{dirName}/{argsFileName}";
            }
        }

        return null;
    }

    /// <summary>
    /// Determines if a Minecraft version uses modern Forge (1.17+).
    /// </summary>
    private static bool IsModernForge(string mcVersion)
    {
        var parts = mcVersion.Split('.');
        if (parts.Length < 2 || !int.TryParse(parts[1], out var minor))
            return true;

        return minor >= 17;
    }

    /// <summary>
    /// Gets the required Java version for a Minecraft version.
    /// </summary>
    private static int GetRequiredJavaVersion(string mcVersion)
    {
        var parts = mcVersion.Split('.');
        if (parts.Length < 2 || !int.TryParse(parts[1], out var minor))
            return 21;

        // MC 1.21+ requires Java 21
        if (minor >= 21) return 21;
        // MC 1.20.5+ requires Java 21
        if (minor == 20 && parts.Length >= 3 && int.TryParse(parts[2], out var patch) && patch >= 5)
            return 21;
        // MC 1.18-1.20.4 requires Java 17
        if (minor >= 18) return 17;
        // MC 1.17 requires Java 16
        if (minor == 17) return 16;
        // Older versions use Java 8
        return 8;
    }

    private static List<string> BuildEnvironmentVariables(DockerConfiguration config)
    {
        return config.EnvironmentVariables
            .Select(kv => $"{kv.Key}={kv.Value}")
            .ToList();
    }

    private Dictionary<string, IList<PortBinding>> BuildPortBindings(List<PortMapping> ports, GameServer server, bool includeServerPorts = false)
    {
        var bindings = new Dictionary<string, IList<PortBinding>>();

        // Add configured port mappings
        foreach (var port in ports)
        {
            var containerPort = $"{port.ContainerPort}/{port.Protocol}";
            bindings[containerPort] = new List<PortBinding>
            {
                new()
                {
                    HostIP = server.BindAddress,
                    HostPort = port.HostPort.ToString()
                }
            };
        }

        // For modpack servers, automatically add primary port and RCON port
        // Container uses default internal ports (25565 game, 25575 RCON); host port is user-specified
        if (includeServerPorts)
        {
            // Add primary game port: map user's host port to container's internal port 25565
            const int defaultGamePort = 25565;
            var primaryPortKey = $"{defaultGamePort}/tcp";
            if (!bindings.ContainsKey(primaryPortKey))
            {
                bindings[primaryPortKey] = new List<PortBinding>
                {
                    new() { HostIP = server.BindAddress, HostPort = server.PrimaryPort.ToString() }
                };
                _logger.LogDebug("Auto-added primary port mapping: {HostPort} -> {ContainerPort}",
                    server.PrimaryPort, defaultGamePort);
            }

            // Add RCON port if configured: map user's host port to container's internal port 25575
            if (server.RconConfig is { Enabled: true })
            {
                const int defaultRconPort = 25575;
                var rconPortKey = $"{defaultRconPort}/tcp";
                if (!bindings.ContainsKey(rconPortKey))
                {
                    bindings[rconPortKey] = new List<PortBinding>
                    {
                        new() { HostIP = server.BindAddress, HostPort = server.RconConfig.Port.ToString() }
                    };
                    _logger.LogDebug("Auto-added RCON port mapping: {HostPort} -> {ContainerPort}",
                        server.RconConfig.Port, defaultRconPort);
                }
            }

        }

        return bindings;
    }

    private List<string> BuildVolumeMounts(List<VolumeMount> volumes)
    {
        return volumes.Select(v =>
        {
            var mode = v.ReadOnly ? "ro" : "rw";
            // Named volumes use the volume name directly, bind mounts use the host path
            // For bind mounts, translate local paths to host paths for Docker daemon
            var source = v.IsNamedVolume ? v.HostPath : _pathResolver.ResolveHostPath(v.HostPath);
            return $"{source}:{v.ContainerPath}:{mode}";
        }).ToList();
    }

    private List<string> BuildVolumeMountsWithSystemVolumes(List<VolumeMount> volumes)
    {
        var mounts = BuildVolumeMounts(volumes);

        // On Linux, mount machine-id for games that require machine identification
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && File.Exists("/etc/machine-id"))
        {
            mounts.Add("/etc/machine-id:/etc/machine-id:ro");
        }

        return mounts;
    }

    /// <summary>
    /// Ensures all named volumes in the configuration exist, creating them if necessary.
    /// Named volumes provide better I/O performance than bind mounts on Windows Docker Desktop.
    /// </summary>
    private async Task EnsureNamedVolumesExistAsync(List<VolumeMount> volumes, CancellationToken ct)
    {
        var namedVolumes = volumes.Where(v => v.IsNamedVolume).ToList();
        if (namedVolumes.Count == 0)
        {
            return;
        }

        foreach (var volume in namedVolumes)
        {
            try
            {
                // Check if volume already exists
                var existingVolumes = await _docker.Volumes.ListAsync(ct).ConfigureAwait(false);
                var exists = existingVolumes.Volumes?.Any(v => v.Name == volume.HostPath) ?? false;

                if (!exists)
                {
                    _logger.LogInformation("Creating named Docker volume: {VolumeName}", volume.HostPath);
                    await _docker.Volumes.CreateAsync(
                        new VolumesCreateParameters
                        {
                            Name = volume.HostPath,
                            Labels = new Dictionary<string, string>
                            {
                                ["nebula.managed"] = "true",
                                ["nebula.purpose"] = "game-server-data"
                            }
                        },
                        ct).ConfigureAwait(false);
                    _logger.LogInformation("Created named Docker volume: {VolumeName}", volume.HostPath);
                }
                else
                {
                    _logger.LogDebug("Named volume already exists: {VolumeName}", volume.HostPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to ensure named volume exists: {VolumeName}", volume.HostPath);
                // Don't throw - Docker will create the volume automatically if it doesn't exist
            }
        }
    }

    private static Docker.DotNet.Models.RestartPolicy BuildRestartPolicy(Domain.ValueObjects.RestartPolicy policy)
    {
        return policy switch
        {
            Domain.ValueObjects.RestartPolicy.None => new Docker.DotNet.Models.RestartPolicy { Name = RestartPolicyKind.No },
            Domain.ValueObjects.RestartPolicy.Always => new Docker.DotNet.Models.RestartPolicy { Name = RestartPolicyKind.Always },
            Domain.ValueObjects.RestartPolicy.OnFailure => new Docker.DotNet.Models.RestartPolicy { Name = RestartPolicyKind.OnFailure, MaximumRetryCount = 5 },
            Domain.ValueObjects.RestartPolicy.UnlessStopped => new Docker.DotNet.Models.RestartPolicy { Name = RestartPolicyKind.UnlessStopped },
            _ => new Docker.DotNet.Models.RestartPolicy { Name = RestartPolicyKind.No }
        };
    }

    private static void ApplyResourceLimits(HostConfig hostConfig, ResourceLimits? limits)
    {
        if (limits is null)
        {
            return;
        }

        if (limits.MaxMemoryMb.HasValue)
        {
            hostConfig.Memory = limits.MaxMemoryMb.Value * 1024L * 1024L;
            // Set memory swap to same as memory to prevent swap
            hostConfig.MemorySwap = hostConfig.Memory;
        }

        if (limits.MaxCpuPercent.HasValue)
        {
            // Docker uses NanoCPUs for CPU limiting
            // 1 CPU = 1_000_000_000 NanoCPUs
            // For example, 50% of 1 CPU = 500_000_000 NanoCPUs
            hostConfig.NanoCPUs = limits.MaxCpuPercent.Value * 10_000_000L;
        }
    }

    private static string GenerateContainerName(GameServer server)
    {
        // Create a unique container name based on server ID
        var shortId = server.Id.ToString("N")[..8];
        var safeName = new string(server.Name
            .ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c) || c == '-')
            .Take(20)
            .ToArray());

        return $"nebula-{safeName}-{shortId}";
    }

    private async Task<ContainerListResponse?> GetContainerInfoAsync(string containerId, CancellationToken ct)
    {
        try
        {
            var containers = await _docker.Containers.ListContainersAsync(
                new ContainersListParameters
                {
                    All = true,
                    Filters = new Dictionary<string, IDictionary<string, bool>>
                    {
                        ["id"] = new Dictionary<string, bool> { [containerId] = true }
                    }
                },
                ct).ConfigureAwait(false);

            return containers.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get container info for {ContainerId}", containerId);
            return null;
        }
    }

    private async Task<bool> IsDockerAvailableAsync(CancellationToken ct)
    {
        try
        {
            await _docker.System.PingAsync(ct).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> ImageExistsAsync(string imageName, CancellationToken ct)
    {
        try
        {
            var images = await _docker.Images.ListImagesAsync(
                new ImagesListParameters
                {
                    Filters = new Dictionary<string, IDictionary<string, bool>>
                    {
                        ["reference"] = new Dictionary<string, bool> { [imageName] = true }
                    }
                },
                ct).ConfigureAwait(false);

            return images.Count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check if image {ImageName} exists", imageName);
            return false;
        }
    }

    private async Task<ContainerStatsResponse?> GetContainerStatsAsync(string containerId, CancellationToken ct)
    {
        try
        {
            ContainerStatsResponse? stats = null;

            await _docker.Containers.GetContainerStatsAsync(
                containerId,
                new ContainerStatsParameters { Stream = false },
                new Progress<ContainerStatsResponse>(s => stats = s),
                ct).ConfigureAwait(false);

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get stats for container {ContainerId}", containerId);
            return null;
        }
    }

    private static bool IsWritableDirectory(string path)
    {
        try
        {
            var testFile = Path.Combine(path, $".nebula-perm-test-{Guid.NewGuid():N}");
            using var stream = new FileStream(
                testFile,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1,
                FileOptions.DeleteOnClose);
            stream.WriteByte(0);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task EnsureHostPathOwnershipAsync(GameServer server, DockerConfiguration config, CancellationToken ct)
    {
        if (!OperatingSystem.IsLinux())
            return;

        var containerUser = GetContainerUser();
        if (string.IsNullOrWhiteSpace(containerUser))
            return;

        var parts = containerUser.Split(':', 2);
        if (parts.Length != 2)
            return;

        var uid = parts[0];
        var gid = parts[1];

        var hostPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var volume in config.Volumes.Where(v => !v.IsNamedVolume && !string.IsNullOrWhiteSpace(v.HostPath)))
        {
            // Skip file mounts (e.g. serverconfig.txt) — only process directory paths.
            // File mounts are handled by EnsureBindMountFilesExist.
            if (Path.HasExtension(volume.HostPath) || Path.HasExtension(volume.ContainerPath))
                continue;

            hostPaths.Add(volume.HostPath);
        }

        if (!string.IsNullOrWhiteSpace(server.InstallPath))
        {
            hostPaths.Add(server.InstallPath);
        }

        foreach (var hostPath in hostPaths)
        {
            try
            {
                Directory.CreateDirectory(hostPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to ensure host path exists: {Path}", hostPath);
                continue;
            }

            if (IsWritableDirectory(hostPath))
                continue;

            _logger.LogWarning(
                "Host path {Path} is not writable by the panel user. Attempting to fix permissions...",
                hostPath);

            await FixOwnershipWithHelperAsync(hostPath, uid, gid, ct).ConfigureAwait(false);

            if (!IsWritableDirectory(hostPath))
            {
                _logger.LogError("Host path {Path} is still not writable after permission fix attempt", hostPath);
            }
        }
    }

    /// <summary>
    /// Ensures that bind mount sources targeting files inside the container exist as files
    /// (not directories) on the host. Docker creates directories for missing bind mount paths,
    /// which causes "not a directory" errors when the container expects a file.
    /// If a directory exists at a file mount path (from a previous failed run), it is removed.
    /// </summary>
    private void EnsureBindMountFilesExist(List<VolumeMount> volumes)
    {
        foreach (var volume in volumes)
        {
            if (volume.IsNamedVolume || string.IsNullOrWhiteSpace(volume.HostPath))
                continue;

            // Heuristic: if the container path has a file extension, the mount target is a file.
            // Also check if the host path has an extension (e.g. serverconfig.txt).
            var containerHasExtension = Path.HasExtension(volume.ContainerPath);
            var hostHasExtension = Path.HasExtension(volume.HostPath);

            if (!containerHasExtension && !hostHasExtension)
                continue;

            // If the file already exists (with content or empty), leave it alone.
            if (File.Exists(volume.HostPath))
                continue;

            // If a directory was mistakenly created at this path (e.g. by a previous
            // Docker run or EnsureHostPathOwnershipAsync), remove it so Docker doesn't
            // try to mount a directory where a file is expected.
            if (Directory.Exists(volume.HostPath))
            {
                try
                {
                    Directory.Delete(volume.HostPath, recursive: true);
                    _logger.LogDebug("Removed directory at file mount source path {Path}", volume.HostPath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to remove directory at file mount path {Path}", volume.HostPath);
                    continue;
                }
            }

            // Create the file to prevent Docker from creating a directory at this path.
            // The installer should have generated this file with content — if it didn't,
            // the container will likely fail with a config error, which is more actionable
            // than Docker's cryptic "not a directory" error.
            try
            {
                var parentDir = Path.GetDirectoryName(volume.HostPath);
                if (!string.IsNullOrEmpty(parentDir))
                    Directory.CreateDirectory(parentDir);

                File.Create(volume.HostPath).Dispose();
                _logger.LogWarning(
                    "Bind mount source file {Path} did not exist and was created as empty. " +
                    "The server installer may not have run. Re-install the server to generate the config.",
                    volume.HostPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create file for bind mount at {Path}", volume.HostPath);
            }
        }
    }

    private const string PermissionHelperImage = "alpine:3.20";

    private async Task EnsurePermissionHelperImageAsync(CancellationToken ct)
    {
        if (await ImageExistsAsync(PermissionHelperImage, ct).ConfigureAwait(false))
            return;

        _logger.LogInformation("Pulling helper image {Image} to fix permissions", PermissionHelperImage);
        await _docker.Images.CreateImageAsync(
            new ImagesCreateParameters
            {
                FromImage = "alpine",
                Tag = "3.20"
            },
            null,
            new Progress<JSONMessage>(),
            ct).ConfigureAwait(false);
    }

    private async Task FixOwnershipWithHelperAsync(string hostPath, string uid, string gid, CancellationToken ct)
    {
        await EnsurePermissionHelperImageAsync(ct).ConfigureAwait(false);

        var createParams = new CreateContainerParameters
        {
            Image = PermissionHelperImage,
            Cmd = ["sh", "-c", $"chown -R {uid}:{gid} /target"],
            User = "0:0",
            HostConfig = new HostConfig
            {
                Binds = [$"{hostPath}:/target"]
            }
        };

        var response = await _docker.Containers.CreateContainerAsync(createParams, ct).ConfigureAwait(false);

        try
        {
            await _docker.Containers.StartContainerAsync(response.ID, new ContainerStartParameters(), ct)
                .ConfigureAwait(false);
            await _docker.Containers.WaitContainerAsync(response.ID, ct).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                await _docker.Containers.RemoveContainerAsync(
                    response.ID,
                    new ContainerRemoveParameters { Force = true },
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove permission helper container {ContainerId}", response.ID);
            }
        }
    }



    private void StopLogStreaming(Guid serverId)
    {
        if (_containerStore.TryRemove(serverId, out var managedContainer))
        {
            managedContainer?.Dispose();
        }
    }

    private async IAsyncEnumerable<string> ParseDockerLogStreamAsync(
        MultiplexedStream stream,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var buffer = new byte[4096];

        while (!ct.IsCancellationRequested)
        {
            var result = await stream.ReadOutputAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false);

            if (result.EOF)
            {
                yield break;
            }

            if (result.Count > 0)
            {
                var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                {
                    // Filter out RCON connection/disconnection noise
                    if (ShouldFilterLogLine(line))
                    {
                        continue;
                    }

                    var cleanLine = StripAnsiCodes(StripDockerTimestamp(line.TrimEnd()));
                    yield return $"[{DateTime.UtcNow:HH:mm:ss}] {cleanLine}";
                }
            }
        }
    }

    /// <summary>
    /// Checks if a log line should be filtered out (RCON noise, etc.)
    /// </summary>
    internal static bool ShouldFilterLogLine(string line)
    {
        // Filter RCON connection/disconnection logs
        if (line.Contains("RCON Listener") && line.Contains("started"))
            return true;
        if (line.Contains("RCON Client") && line.Contains("shutting down"))
            return true;
        if (line.Contains("Thread RCON Client") && line.Contains("started"))
            return true;

        return false;
    }

    /// <summary>
    /// Strips ANSI escape sequences from a string.
    /// These are used for terminal colors/formatting but display as garbage in the UI.
    /// </summary>
    internal static string StripAnsiCodes(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        // ANSI escape sequences: ESC[ followed by parameters and a letter
        // Pattern: \x1b\[[0-9;]*[a-zA-Z] or \x1b\]...\x07 (OSC sequences)
        return System.Text.RegularExpressions.Regex.Replace(
            input,
            @"\x1b\[[0-9;]*[a-zA-Z]|\x1b\][^\x07]*\x07",
            string.Empty);
    }

    /// <summary>
    /// Strips Docker's ISO 8601 timestamp prefix from log lines.
    /// Docker adds timestamps like "2026-01-15T17:47:29.439949047Z " when Timestamps=true.
    /// </summary>
    internal static string StripDockerTimestamp(string input)
    {
        if (string.IsNullOrEmpty(input) || input.Length < 31)
            return input;

        // Docker timestamp format: YYYY-MM-DDTHH:MM:SS.nnnnnnnnnZ followed by space
        // Example: 2026-01-15T17:47:29.439949047Z
        if (input[4] == '-' && input[7] == '-' && input[10] == 'T' &&
            input[13] == ':' && input[16] == ':' && input[19] == '.')
        {
            var spaceIndex = input.IndexOf(' ');
            if (spaceIndex > 20 && spaceIndex < 35)
            {
                return input[(spaceIndex + 1)..];
            }
        }

        return input;
    }

    private async Task SendCommandViaAttachAsync(string containerId, string command, CancellationToken ct)
    {
        using var stream = await _docker.Containers.AttachContainerAsync(
            containerId,
            false, // tty
            new ContainerAttachParameters
            {
                Stdin = true,
                Stream = true
            },
            ct).ConfigureAwait(false);

        var lineEnding = OperatingSystem.IsWindows() ? "\r\n" : "\n";
        _logger.LogDebug("Sending command via attach with line ending {LineEnding} to container {ContainerId}",
            lineEnding == "\r\n" ? "CRLF" : "LF", containerId);
        var bytes = Encoding.UTF8.GetBytes(command + lineEnding);
        await stream.WriteAsync(bytes, 0, bytes.Length, ct).ConfigureAwait(false);
    }

    private async Task SendCommandViaExecAsync(string containerId, string command, bool tty, CancellationToken ct)
    {
        // For game servers, we need to send commands to the running process's stdin
        // Write directly to /proc/1/fd/0 (the stdin of PID 1)
        // For TTY mode, use \r\n line ending; for non-TTY, use \n
        var escapedCommand = command.Replace("\\", "\\\\").Replace("'", "'\\''");
        var lineEnding = tty ? "\\r\\n" : "\\n";

        var execCreateResponse = await _docker.Exec.ExecCreateContainerAsync(
            containerId,
            new ContainerExecCreateParameters
            {
                // Use printf for better control over line endings
                Cmd = ["sh", "-c", $"printf '%s{lineEnding}' '{escapedCommand}' > /proc/1/fd/0"],
                AttachStdout = false,
                AttachStderr = false
            },
            ct).ConfigureAwait(false);

        await _docker.Exec.StartContainerExecAsync(
            execCreateResponse.ID,
            ct).ConfigureAwait(false);

        _logger.LogInformation("Sent command via exec to /proc/1/fd/0 for container {ContainerId}", containerId);
    }

    /// <summary>
    /// Sends a command to a container that runs its server inside tmux.
    /// Uses the container's <c>inject</c> script: <c>docker exec &lt;container&gt; inject "command"</c>.
    /// This is required for images like jacobsmile/tmodloader1.4.
    /// </summary>
    private async Task SendCommandViaTmuxInjectAsync(string containerId, string command, CancellationToken ct)
    {
        var execCreateResponse = await _docker.Exec.ExecCreateContainerAsync(
            containerId,
            new ContainerExecCreateParameters
            {
                Cmd = ["inject", command],
                AttachStdout = true,
                AttachStderr = true
            },
            ct).ConfigureAwait(false);

        await _docker.Exec.StartContainerExecAsync(
            execCreateResponse.ID,
            ct).ConfigureAwait(false);

        _logger.LogInformation("Sent command via tmux inject for container {ContainerId}", containerId);
    }

    public void Dispose()
    {
        _docker.Dispose();
    }
}

/// <summary>
/// Manages a Docker container instance, including log streaming and stdin.
/// </summary>
internal sealed class ManagedContainer : IDisposable
{
    private readonly Guid _serverId;
    private readonly string _containerId;
    private readonly DockerClient _docker;
    private readonly ILogger _logger;
    private readonly Channel<string> _outputChannel;
    private readonly bool _tty;
    private CancellationTokenSource? _logCts;
    private Task? _logTask;
    private MultiplexedStream? _stdinStream;
    private readonly SemaphoreSlim _stdinLock = new(1, 1);
    private bool _disposed;

    public ManagedContainer(Guid serverId, string containerId, DockerClient docker, ILogger logger, bool tty = false)
    {
        _serverId = serverId;
        _containerId = containerId;
        _docker = docker;
        _logger = logger;
        _tty = tty;
        _outputChannel = Channel.CreateBounded<string>(new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = false,
            SingleWriter = true
        });
    }

    /// <summary>
    /// Attaches to the container's stdin stream. Must be called BEFORE starting the container.
    /// </summary>
    public async Task AttachStdinAsync(CancellationToken ct = default)
    {
        try
        {
            // For non-TTY mode: stdin is a regular pipe, we just need stdin attached
            // For TTY mode: we need stdin AND stdout together because TTY multiplexes all streams
            _stdinStream = await _docker.Containers.AttachContainerAsync(
                _containerId,
                tty: _tty,
                new ContainerAttachParameters
                {
                    Stdin = true,
                    Stdout = _tty,  // Enable stdout for TTY mode for bidirectional communication
                    Stderr = _tty,  // Enable stderr for TTY mode
                    Stream = true
                },
                ct).ConfigureAwait(false);

            _logger.LogInformation("Attached stdin stream to container {ContainerId} (TTY={Tty})", _containerId, _tty);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to attach stdin to container {ContainerId}, commands may not work", _containerId);
        }
    }

    /// <summary>
    /// Starts log capture. Should be called AFTER the container is started.
    /// </summary>
    public void StartLogCapture()
    {
        _logCts = new CancellationTokenSource();
        _logTask = CaptureLogsAsync(_logCts.Token);
    }

    /// <summary>
    /// Legacy method that attaches stdin and starts log capture.
    /// For running containers (e.g., restart case).
    /// </summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        await AttachStdinAsync(ct).ConfigureAwait(false);
        StartLogCapture();
    }

    public async Task SendCommandAsync(string command, CancellationToken ct = default)
    {
        await _stdinLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _logger.LogInformation("Sending command to container {ContainerId} (TTY={Tty}, Platform={Platform}): '{Command}'",
                _containerId, _tty, OperatingSystem.IsWindows() ? "Windows" : "Linux", command);

            // On Windows, Docker.DotNet's AttachContainerAsync stdin is broken
            // (see https://github.com/dotnet/Docker.DotNet/issues/618)
            // Use exec API directly instead - it reliably writes to /proc/1/fd/0
            if (OperatingSystem.IsWindows())
            {
                _logger.LogDebug("Using exec API on Windows for container {ContainerId}", _containerId);
                await SendCommandViaExecApiAsync(command, ct).ConfigureAwait(false);
                return;
            }

            // Linux: stdin attach works reliably
            if (_stdinStream is not null)
            {
                var bytes = Encoding.UTF8.GetBytes(command + "\n");
                await _stdinStream.WriteAsync(bytes, 0, bytes.Length, ct).ConfigureAwait(false);
                _logger.LogInformation("Command sent via attached stdin stream to container {ContainerId}", _containerId);
            }
            else
            {
                _logger.LogWarning("No stdin stream attached, using exec API fallback");
                await SendCommandViaExecApiAsync(command, ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send command to container {ContainerId}", _containerId);
            throw;
        }
        finally
        {
            _stdinLock.Release();
        }
    }

    private async Task SendCommandViaDockerCliAsync(string command, CancellationToken ct)
    {
        var escapedCommand = command.Replace("'", "'\\''");
        var psi = new ProcessStartInfo
        {
            FileName = "docker",
            Arguments = $"exec -i {_containerId} sh -c 'printf \"%s\\n\" '\"'\"'{escapedCommand}'\"'\"' > /proc/1/fd/0'",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        _logger.LogDebug("Running: docker {Args}", psi.Arguments);

        using var process = Process.Start(psi);
        if (process is null)
        {
            throw new InvalidOperationException("Failed to start docker exec process");
        }

        await process.WaitForExitAsync(ct).ConfigureAwait(false);

        var stdout = await process.StandardOutput.ReadToEndAsync(ct).ConfigureAwait(false);
        var stderr = await process.StandardError.ReadToEndAsync(ct).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            _logger.LogWarning("Docker exec returned exit code {ExitCode}. Stdout: {Stdout}, Stderr: {Stderr}",
                process.ExitCode, stdout, stderr);
        }
        else
        {
            _logger.LogInformation("Command sent via docker exec to container {ContainerId}", _containerId);
        }
    }

    /// <summary>
    /// Sends a command to the container's main process via Docker exec API.
    /// This is more reliable than AttachContainerAsync on Windows.
    /// </summary>
    private async Task SendCommandViaExecApiAsync(string command, CancellationToken ct)
    {
        var escapedCommand = command.Replace("\\", "\\\\").Replace("'", "'\\''");

        var execCreateResponse = await _docker.Exec.ExecCreateContainerAsync(
            _containerId,
            new ContainerExecCreateParameters
            {
                // Write command to PID 1's stdin via /proc/1/fd/0
                Cmd = ["sh", "-c", $"printf '%s\\n' '{escapedCommand}' > /proc/1/fd/0"],
                AttachStdout = false,
                AttachStderr = false
            },
            ct).ConfigureAwait(false);

        await _docker.Exec.StartContainerExecAsync(execCreateResponse.ID, ct).ConfigureAwait(false);

        _logger.LogInformation("Command sent via exec API to /proc/1/fd/0 for container {ContainerId}", _containerId);
    }

    public bool HasStdinStream => _stdinStream is not null;

    public bool IsTty => _tty;

    /// <summary>
    /// Sends a command via docker exec to /proc/1/fd/0.
    /// This is a fallback method when stdin attach doesn't work reliably.
    /// </summary>
    public async Task SendCommandViaExecAsync(string command, CancellationToken ct = default)
    {
        // Escape the command for shell
        var escapedCommand = command.Replace("\\", "\\\\").Replace("'", "'\\''");
        var lineEnding = _tty ? "\\r\\n" : "\\n";

        var execCreateResponse = await _docker.Exec.ExecCreateContainerAsync(
            _containerId,
            new ContainerExecCreateParameters
            {
                Cmd = ["sh", "-c", $"printf '%s{lineEnding}' '{escapedCommand}' > /proc/1/fd/0"],
                AttachStdout = false,
                AttachStderr = false
            },
            ct).ConfigureAwait(false);

        await _docker.Exec.StartContainerExecAsync(
            execCreateResponse.ID,
            ct).ConfigureAwait(false);

        _logger.LogInformation("Sent command via exec to /proc/1/fd/0 for container {ContainerId}", _containerId);
    }

    private async Task CaptureLogsAsync(CancellationToken ct)
    {
        var logParams = new ContainerLogsParameters
        {
            ShowStdout = true,
            ShowStderr = true,
            Follow = true,
            Tail = "100",
            Timestamps = true
        };

        MultiplexedStream? stream = null;

        try
        {
            stream = await _docker.Containers.GetContainerLogsAsync(
                _containerId,
                _tty,
                logParams,
                ct).ConfigureAwait(false);

            var buffer = new byte[4096];

            while (!ct.IsCancellationRequested)
            {
                var result = await stream.ReadOutputAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(false);

                if (result.EOF)
                {
                    break;
                }

                if (result.Count > 0)
                {
                    var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                    {
                        // Filter out RCON connection/disconnection noise
                        if (DockerServerExecutor.ShouldFilterLogLine(line))
                        {
                            continue;
                        }

                        var cleanLine = DockerServerExecutor.StripAnsiCodes(
                            DockerServerExecutor.StripDockerTimestamp(line.TrimEnd()));
                        var timestampedLine = $"[{DateTime.UtcNow:HH:mm:ss}] {cleanLine}";
                        _outputChannel.Writer.TryWrite(timestampedLine);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing logs for container {ContainerId}", _containerId);
        }
        finally
        {
            stream?.Dispose();
        }
    }

    public async IAsyncEnumerable<string> ReadOutputAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var line in _outputChannel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            yield return line;
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _logCts?.Cancel();
        _outputChannel.Writer.TryComplete();

        try
        {
            _logTask?.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Ignore task wait exceptions on dispose
        }

        _logCts?.Dispose();
        _stdinStream?.Dispose();
        _stdinLock.Dispose();
    }
}
