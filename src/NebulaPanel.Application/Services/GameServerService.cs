using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Interface for RCON service to send commands to game servers.
/// </summary>
public interface IRconService
{
    Task<string?> SendCommandAsync(RconConfiguration config, string command, CancellationToken cancellationToken = default);
    Task<bool> TestConnectionAsync(RconConfiguration config, CancellationToken cancellationToken = default);
}

public class GameServerService(
    IGameServerRepository serverRepository,
    IGameRepository gameRepository,
    ISteamCmdService steamCmdService,
    INodeAwareExecutorFactory nodeExecutorFactory,
    IRconService? rconService,
    IImageService imageService,
    IServerActivityService? serverActivityService,
    IEncryptionService encryptionService,
    ILogger<GameServerService> logger,
    IAuditService? auditService = null,
    INodeRepository? nodeRepository = null) : IGameServerService
{
    private readonly IGameServerRepository _serverRepository = serverRepository;
    private readonly IGameRepository _gameRepository = gameRepository;
    private readonly ISteamCmdService _steamCmdService = steamCmdService;
    private readonly INodeAwareExecutorFactory _nodeExecutorFactory = nodeExecutorFactory;
    private readonly IRconService? _rconService = rconService;
    private readonly IImageService _imageService = imageService;
    private readonly IServerActivityService? _serverActivityService = serverActivityService;
    private readonly IEncryptionService _encryptionService = encryptionService;
    private readonly IAuditService? _auditService = auditService;
    private readonly INodeRepository? _nodeRepository = nodeRepository;
    private readonly ILogger<GameServerService> _logger = logger;

    public async Task<IReadOnlyList<GameServerListItemDto>> GetAllServersAsync(CancellationToken cancellationToken = default)
    {
        var servers = await _serverRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return servers.Select(MapToListItemDto).ToList();
    }

    public async Task<IReadOnlyList<GameServerListItemDto>> GetServersByOwnerAsync(Guid ownerId, CancellationToken cancellationToken = default)
    {
        var servers = await _serverRepository.GetByOwnerIdAsync(ownerId, cancellationToken).ConfigureAwait(false);
        return servers.Select(MapToListItemDto).ToList();
    }

    public async Task<Result<GameServerDto>> GetServerByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var server = await _serverRepository.GetByIdWithGameAsync(id, cancellationToken).ConfigureAwait(false);
        if (server is null)
        {
            return Result.Failure<GameServerDto>(Error.NotFound("Server", id.ToString()));
        }

        return MapToDto(server);
    }

    public async Task<Result<GameServerDto>> CreateServerAsync(CreateGameServerRequest request, Guid ownerId, CancellationToken cancellationToken = default)
    {
        // Validate game exists
        var game = await _gameRepository.GetByIdAsync(request.GameId, cancellationToken).ConfigureAwait(false);
        if (game is null)
        {
            return Result.Failure<GameServerDto>(Error.NotFound("Game", request.GameId.ToString()));
        }

        // Validate unique name per owner
        if (await _serverRepository.NameExistsForOwnerAsync(request.Name, ownerId, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<GameServerDto>(Error.AlreadyExists("Server", request.Name));
        }

        // Validate port availability
        var bindAddress = request.BindAddress ?? "0.0.0.0";
        if (await _serverRepository.IsPortInUseAsync(request.PrimaryPort, bindAddress, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<GameServerDto>(Error.PortInUse(request.PrimaryPort));
        }

        // Validate deployment type configuration
        if (request.DeploymentType == ServerDeploymentType.Docker)
        {
            if (request.DockerConfig is null)
            {
                return Result.Failure<GameServerDto>(Error.Validation("Docker configuration is required for Docker deployment type."));
            }
            if (!game.SupportsDocker)
            {
                return Result.Failure<GameServerDto>(Error.Validation($"Game '{game.Name}' does not support Docker deployment."));
            }
        }
        else if (request.DeploymentType == ServerDeploymentType.Native)
        {
            if (request.NativeConfig is null)
            {
                return Result.Failure<GameServerDto>(Error.Validation("Native configuration is required for Native deployment type."));
            }
        }

        // Validate node exists if specified
        if (request.NodeId.HasValue && _nodeRepository is not null)
        {
            var nodeExists = await _nodeRepository.ExistsAsync(request.NodeId.Value, cancellationToken).ConfigureAwait(false);
            if (!nodeExists)
            {
                return Result.Failure<GameServerDto>(Error.NotFound("Node", request.NodeId.Value.ToString()));
            }
        }

        var serverId = Guid.NewGuid();

        // Handle icon URL - download if remote
        string? iconPath = null;
        if (!string.IsNullOrWhiteSpace(request.IconUrl))
        {
            if (_imageService.IsRemoteUrl(request.IconUrl))
            {
                iconPath = await _imageService.EnsureLocalAsync(
                    request.IconUrl,
                    "servers",
                    serverId.ToString(),
                    cancellationToken).ConfigureAwait(false);

                if (iconPath is null)
                {
                    _logger.LogWarning("Failed to download server icon from {Url}, using URL directly", request.IconUrl);
                    iconPath = request.IconUrl;
                }
            }
            else
            {
                iconPath = request.IconUrl;
            }
        }

        var server = new GameServer
        {
            Id = serverId,
            Name = request.Name,
            Description = request.Description,
            IconPath = iconPath,
            GameId = request.GameId,
            DeploymentType = request.DeploymentType,
            InstallPath = request.InstallPath,
            PrimaryPort = request.PrimaryPort,
            AdditionalPorts = request.AdditionalPorts ?? [],
            BindAddress = bindAddress,
            Status = ServerStatus.Stopped,
            OwnerId = ownerId,
            DockerConfig = MapDockerConfig(request.DockerConfig),
            NativeConfig = MapNativeConfig(request.NativeConfig),
            RconConfig = MapRconConfig(request.RconConfig),
            ResourceLimits = MapResourceLimits(request.ResourceLimits),
            NodeId = request.NodeId
        };

        EncryptRconPassword(server);

        await _serverRepository.AddAsync(server, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Created server {ServerName} with ID {ServerId} for owner {OwnerId}", server.Name, server.Id, ownerId);

        if (_auditService is not null)
        {
            await _auditService.LogServerOperationAsync(
                SecurityEventType.ServerCreated, ownerId, string.Empty,
                server.Id, server.Name, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        // Reload with includes for the DTO
        server = await _serverRepository.GetByIdWithGameAsync(server.Id, cancellationToken).ConfigureAwait(false);
        return MapToDto(server!);
    }

    public async Task<Result<GameServerDto>> UpdateServerAsync(Guid id, UpdateGameServerRequest request, CancellationToken cancellationToken = default)
    {
        var server = await _serverRepository.GetByIdWithGameAsync(id, cancellationToken).ConfigureAwait(false);
        if (server is null)
        {
            return Result.Failure<GameServerDto>(Error.NotFound("Server", id.ToString()));
        }

        // Validate unique name per owner
        if (await _serverRepository.NameExistsForOwnerAsync(request.Name, server.OwnerId, id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<GameServerDto>(Error.AlreadyExists("Server", request.Name));
        }

        // Validate port availability
        var bindAddress = request.BindAddress ?? server.BindAddress;
        if (await _serverRepository.IsPortInUseAsync(request.PrimaryPort, bindAddress, id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<GameServerDto>(Error.PortInUse(request.PrimaryPort));
        }

        // For Docker servers, if port changes, we need to remove the existing container
        // Docker containers can't have their port bindings changed after creation
        var portChanged = server.PrimaryPort != request.PrimaryPort;
        var rconPortChanged = server.RconConfig?.Port != request.RconConfig?.Port;
        if (server.DeploymentType == ServerDeploymentType.Docker &&
            !string.IsNullOrEmpty(server.DockerContainerId) &&
            (portChanged || rconPortChanged))
        {
            try
            {
                var executor = _nodeExecutorFactory.GetExecutor(server);
                await executor.CleanupAsync(server, cancellationToken).ConfigureAwait(false);
                server.DockerContainerId = null; // Clear container ID so a new one is created on next start
                _logger.LogInformation("Removed Docker container for server {ServerName} due to port change", server.Name);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove Docker container for server {ServerName} during port update", server.Name);
                // Continue anyway - the old container will be orphaned but we can still update the server
            }
        }

        // Handle icon URL update - null = keep existing, empty = clear, URL = download/set
        if (request.IconUrl is not null)
        {
            if (string.IsNullOrWhiteSpace(request.IconUrl))
            {
                server.IconPath = null; // Clear icon
            }
            else if (request.IconUrl != server.IconPath) // Only process if changed
            {
                if (_imageService.IsRemoteUrl(request.IconUrl))
                {
                    var iconPath = await _imageService.EnsureLocalAsync(
                        request.IconUrl,
                        "servers",
                        id.ToString(),
                        cancellationToken).ConfigureAwait(false);

                    server.IconPath = iconPath ?? request.IconUrl; // Fallback to URL if download fails
                    if (iconPath is null)
                    {
                        _logger.LogWarning("Failed to download server icon from {Url}, using URL directly", request.IconUrl);
                    }
                }
                else
                {
                    server.IconPath = request.IconUrl;
                }
            }
        }

        // Validate node exists if specified
        if (request.NodeId.HasValue && _nodeRepository is not null)
        {
            var nodeExists = await _nodeRepository.ExistsAsync(request.NodeId.Value, cancellationToken).ConfigureAwait(false);
            if (!nodeExists)
            {
                return Result.Failure<GameServerDto>(Error.NotFound("Node", request.NodeId.Value.ToString()));
            }
        }

        server.NodeId = request.NodeId;
        server.Name = request.Name;
        server.Description = request.Description;
        server.InstallPath = request.InstallPath;
        server.PrimaryPort = request.PrimaryPort;
        server.AdditionalPorts = request.AdditionalPorts ?? [];
        server.BindAddress = bindAddress;
        server.DockerConfig = MapDockerConfig(request.DockerConfig) ?? server.DockerConfig;
        server.NativeConfig = MapNativeConfig(request.NativeConfig) ?? server.NativeConfig;
        server.RconConfig = MapRconConfig(request.RconConfig) ?? server.RconConfig;
        server.ResourceLimits = MapResourceLimits(request.ResourceLimits) ?? server.ResourceLimits;

        EncryptRconPassword(server);

        // Update explicit port mappings in DockerConfig when ports change
        if (server.DeploymentType == ServerDeploymentType.Docker && server.DockerConfig?.Ports is { Count: > 0 })
        {
            foreach (var mapping in server.DockerConfig.Ports)
            {
                // Update game port mapping (container port 25565)
                if (mapping.ContainerPort == 25565)
                {
                    mapping.HostPort = request.PrimaryPort;
                }
                // Update RCON port mapping (container port 25575)
                else if (mapping.ContainerPort == 25575 && request.RconConfig?.Port is { } rconPort)
                {
                    mapping.HostPort = rconPort;
                }
            }
        }

        await _serverRepository.UpdateAsync(server, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Updated server {ServerName} with ID {ServerId}", server.Name, server.Id);

        return MapToDto(server);
    }

    public async Task<Result> DeleteServerAsync(Guid id, bool deleteFiles = false, bool deleteContainer = false, CancellationToken cancellationToken = default)
    {
        var server = await _serverRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (server is null)
        {
            return Result.Failure(Error.NotFound("Server", id.ToString()));
        }

        // Only allow deletion of stopped, unknown, or crashed servers
        if (server.Status != ServerStatus.Stopped && server.Status != ServerStatus.Unknown && server.Status != ServerStatus.Crashed)
        {
            return Result.Failure(Error.ServerRunning("delete"));
        }

        var serverName = server.Name;
        var installPath = server.InstallPath;

        // Remove Docker container if requested
        if (deleteContainer && server.DeploymentType == ServerDeploymentType.Docker && !string.IsNullOrEmpty(server.DockerContainerId))
        {
            try
            {
                var executor = _nodeExecutorFactory.GetExecutor(server);
                await executor.CleanupAsync(server, cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Removed Docker container for server {ServerName}", serverName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to remove Docker container for server {ServerName}, continuing with deletion", serverName);
            }
        }

        // Delete from database first
        await _serverRepository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Deleted server {ServerName} with ID {ServerId} from database", serverName, id);

        if (_auditService is not null)
        {
            await _auditService.LogServerOperationAsync(
                SecurityEventType.ServerDeleted, server.OwnerId, string.Empty,
                id, serverName, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        // Delete server files if requested
        if (deleteFiles && !string.IsNullOrEmpty(installPath) && Directory.Exists(installPath))
        {
            try
            {
                Directory.Delete(installPath, recursive: true);
                _logger.LogInformation("Deleted server files at {InstallPath}", installPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete server files at {InstallPath}", installPath);
                // Don't fail the operation - the database entry is already deleted
                return Result.Success(); // Could return a warning here if needed
            }
        }

        return Result.Success();
    }

    public async Task<Result> StartServerAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var server = await _serverRepository.GetByIdWithGameAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (server is null)
        {
            return Result.Failure(Error.NotFound("Server", serverId.ToString()));
        }

        if (server.Status == ServerStatus.Running || server.Status == ServerStatus.Starting)
        {
            return Result.Failure(Error.ServerRunning("start"));
        }

        if (server.Status == ServerStatus.Installing || server.Status == ServerStatus.Updating)
        {
            return Result.Failure(Error.InvalidOperation($"Cannot start server while it is {server.Status.ToString().ToLowerInvariant()}."));
        }

        _logger.LogInformation("Starting server {ServerName} ({ServerId})", server.Name, server.Id);

        try
        {
            var executor = _nodeExecutorFactory.GetExecutor(server);

            server.Status = ServerStatus.Starting;
            try
            {
                await _serverRepository.UpdateAsync(server, cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning("Concurrent modification detected when starting server {ServerId}", serverId);
                return Result.Failure(Error.Conflict("Server state was modified by another request. Please try again."));
            }

            var success = await executor.StartAsync(server, cancellationToken).ConfigureAwait(false);

            if (success)
            {
                server.Status = ServerStatus.Running;
                server.LastStarted = DateTime.UtcNow;
                try
                {
                    await _serverRepository.UpdateAsync(server, cancellationToken).ConfigureAwait(false);
                }
                catch (DbUpdateConcurrencyException)
                {
                    _logger.LogWarning("Concurrent modification detected when updating server {ServerId} after start", serverId);
                }
                _logger.LogInformation("Successfully started server {ServerName}", server.Name);

                // Log activity
                if (_serverActivityService is not null)
                {
                    await _serverActivityService.LogActivityAsync(
                        server.Id,
                        ServerActivityType.Started,
                        $"Server '{server.Name}' started successfully",
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                }

                if (_auditService is not null)
                {
                    await _auditService.LogServerOperationAsync(
                        SecurityEventType.ServerStarted, server.OwnerId, string.Empty,
                        server.Id, server.Name, cancellationToken: cancellationToken).ConfigureAwait(false);
                }

                return Result.Success();
            }
            else
            {
                server.Status = ServerStatus.Stopped;
                try
                {
                    await _serverRepository.UpdateAsync(server, cancellationToken).ConfigureAwait(false);
                }
                catch (DbUpdateConcurrencyException)
                {
                    _logger.LogWarning("Concurrent modification when resetting server {ServerId} status after failed start", serverId);
                }
                _logger.LogError("Failed to start server {ServerName}", server.Name);
                return Result.Failure(Error.ExternalService("ServerExecutor", $"Failed to start server '{server.Name}'."));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting server {ServerName}", server.Name);
            server.Status = ServerStatus.Stopped;
            try
            {
                await _serverRepository.UpdateAsync(server, CancellationToken.None).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning("Concurrent modification when resetting server {ServerId} status after error", serverId);
            }
            return Result.Failure(Error.FromException(ex));
        }
    }

    public async Task<Result> StopServerAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var server = await _serverRepository.GetByIdWithGameAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (server is null)
        {
            return Result.Failure(Error.NotFound("Server", serverId.ToString()));
        }

        if (server.Status == ServerStatus.Stopped || server.Status == ServerStatus.Unknown)
        {
            return Result.Failure(Error.ServerStopped());
        }

        if (server.Status == ServerStatus.Stopping)
        {
            return Result.Failure(Error.InvalidOperation("Server is already stopping."));
        }

        _logger.LogInformation("Stopping server {ServerName} ({ServerId})", server.Name, server.Id);

        try
        {
            var executor = _nodeExecutorFactory.GetExecutor(server);

            server.Status = ServerStatus.Stopping;
            try
            {
                await _serverRepository.UpdateAsync(server, cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning("Concurrent modification detected when stopping server {ServerId}", serverId);
                return Result.Failure(Error.Conflict("Server state was modified by another request. Please try again."));
            }

            var success = await executor.StopAsync(server, force: false, cancellationToken).ConfigureAwait(false);

            server.Status = ServerStatus.Stopped;
            server.LastStopped = DateTime.UtcNow;
            server.ProcessId = null;
            try
            {
                await _serverRepository.UpdateAsync(server, cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning("Concurrent modification detected when updating server {ServerId} after stop", serverId);
            }

            // Log activity
            if (_serverActivityService is not null)
            {
                await _serverActivityService.LogActivityAsync(
                    server.Id,
                    ServerActivityType.Stopped,
                    $"Server '{server.Name}' stopped",
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            if (_auditService is not null)
            {
                await _auditService.LogServerOperationAsync(
                    SecurityEventType.ServerStopped, server.OwnerId, string.Empty,
                    server.Id, server.Name, cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            if (success)
            {
                _logger.LogInformation("Successfully stopped server {ServerName}", server.Name);
                return Result.Success();
            }
            else
            {
                _logger.LogWarning("Server {ServerName} stop returned false but may have stopped", server.Name);
                return Result.Success();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping server {ServerName}", server.Name);
            server.Status = ServerStatus.Unknown;
            try
            {
                await _serverRepository.UpdateAsync(server, CancellationToken.None).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning("Concurrent modification when resetting server {ServerId} status after error", serverId);
            }
            return Result.Failure(Error.FromException(ex));
        }
    }

    public async Task<Result> RestartServerAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var server = await _serverRepository.GetByIdWithGameAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (server is null)
        {
            return Result.Failure(Error.NotFound("Server", serverId.ToString()));
        }

        if (server.Status == ServerStatus.Installing || server.Status == ServerStatus.Updating)
        {
            return Result.Failure(Error.InvalidOperation($"Cannot restart server while it is {server.Status.ToString().ToLowerInvariant()}."));
        }

        _logger.LogInformation("Restarting server {ServerName} ({ServerId})", server.Name, server.Id);

        try
        {
            var executor = _nodeExecutorFactory.GetExecutor(server);

            server.Status = ServerStatus.Stopping;
            try
            {
                await _serverRepository.UpdateAsync(server, cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning("Concurrent modification detected when restarting server {ServerId}", serverId);
                return Result.Failure(Error.Conflict("Server state was modified by another request. Please try again."));
            }

            var success = await executor.RestartAsync(server, cancellationToken).ConfigureAwait(false);

            if (success)
            {
                server.Status = ServerStatus.Running;
                server.LastStarted = DateTime.UtcNow;
                try
                {
                    await _serverRepository.UpdateAsync(server, cancellationToken).ConfigureAwait(false);
                }
                catch (DbUpdateConcurrencyException)
                {
                    _logger.LogWarning("Concurrent modification detected when updating server {ServerId} after restart", serverId);
                }
                _logger.LogInformation("Successfully restarted server {ServerName}", server.Name);

                // Log activity
                if (_serverActivityService is not null)
                {
                    await _serverActivityService.LogActivityAsync(
                        server.Id,
                        ServerActivityType.Restarted,
                        $"Server '{server.Name}' restarted successfully",
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                }

                return Result.Success();
            }
            else
            {
                server.Status = ServerStatus.Stopped;
                server.LastStopped = DateTime.UtcNow;
                try
                {
                    await _serverRepository.UpdateAsync(server, cancellationToken).ConfigureAwait(false);
                }
                catch (DbUpdateConcurrencyException)
                {
                    _logger.LogWarning("Concurrent modification when resetting server {ServerId} status after failed restart", serverId);
                }
                _logger.LogError("Failed to restart server {ServerName}", server.Name);
                return Result.Failure(Error.ExternalService("ServerExecutor", $"Failed to restart server '{server.Name}'."));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restarting server {ServerName}", server.Name);
            server.Status = ServerStatus.Unknown;
            try
            {
                await _serverRepository.UpdateAsync(server, CancellationToken.None).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning("Concurrent modification when resetting server {ServerId} status after error", serverId);
            }
            return Result.Failure(Error.FromException(ex));
        }
    }

    public async Task<Result> KillServerAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var server = await _serverRepository.GetByIdWithGameAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (server is null)
        {
            return Result.Failure(Error.NotFound("Server", serverId.ToString()));
        }

        if (server.Status == ServerStatus.Stopped || server.Status == ServerStatus.Unknown)
        {
            return Result.Failure(Error.ServerStopped());
        }

        _logger.LogWarning("Force killing server {ServerName} ({ServerId})", server.Name, server.Id);

        try
        {
            var executor = _nodeExecutorFactory.GetExecutor(server);

            var success = await executor.StopAsync(server, force: true, cancellationToken).ConfigureAwait(false);

            server.Status = ServerStatus.Stopped;
            server.LastStopped = DateTime.UtcNow;
            server.ProcessId = null;
            await _serverRepository.UpdateAsync(server, cancellationToken).ConfigureAwait(false);

            // Log activity
            if (_serverActivityService is not null)
            {
                await _serverActivityService.LogActivityAsync(
                    server.Id,
                    ServerActivityType.Killed,
                    $"Server '{server.Name}' force killed",
                    cancellationToken: cancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation("Force killed server {ServerName}", server.Name);
            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error force killing server {ServerName}", server.Name);
            server.Status = ServerStatus.Unknown;
            await _serverRepository.UpdateAsync(server, CancellationToken.None).ConfigureAwait(false);
            return Result.Failure(Error.FromException(ex));
        }
    }

    public async Task<Result<ResourceUsage>> GetResourceUsageAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var server = await _serverRepository.GetByIdWithGameAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (server is null)
        {
            return Result.Failure<ResourceUsage>(Error.NotFound("Server", serverId.ToString()));
        }

        if (server.Status != ServerStatus.Running)
        {
            return Result.Failure<ResourceUsage>(Error.ServerStopped("get resource usage"));
        }

        try
        {
            var executor = _nodeExecutorFactory.GetExecutor(server);
            var usage = await executor.GetResourceUsageAsync(server, cancellationToken).ConfigureAwait(false);
            return Result.Success(usage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting resource usage for server {ServerName}", server.Name);
            return Result.Failure<ResourceUsage>(Error.FromException(ex));
        }
    }

    public async Task<Result<string?>> SendCommandAsync(Guid serverId, string command, CancellationToken cancellationToken = default)
    {
        var server = await _serverRepository.GetByIdWithGameAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (server is null)
        {
            return Result.Failure<string?>(Error.NotFound("Server", serverId.ToString()));
        }

        if (server.Status != ServerStatus.Running)
        {
            return Result.Failure<string?>(Error.ServerStopped("send command"));
        }

        // For Docker servers, prefer RCON when available (stdin is unreliable in containers)
        // For Native servers, prefer stdin (more direct)
        var preferRcon = server.DeploymentType == ServerDeploymentType.Docker;
        var hasRcon = _rconService is not null &&
                      server.RconConfig is not null &&
                      server.RconConfig.Enabled;

        _logger.LogInformation("Command send strategy for {ServerName}: DeploymentType={DeploymentType}, PreferRcon={PreferRcon}, HasRcon={HasRcon}, RconService={RconServiceAvailable}, RconConfig={RconConfigExists}, RconEnabled={RconEnabled}",
            server.Name,
            server.DeploymentType,
            preferRcon,
            hasRcon,
            _rconService is not null,
            server.RconConfig is not null,
            server.RconConfig?.Enabled ?? false);

        if (preferRcon && hasRcon)
        {
            var rconResult = await TrySendViaRconAsync(server, command, cancellationToken).ConfigureAwait(false);
            if (rconResult.IsSuccess)
            {
                return rconResult;
            }
            // RCON failed, try stdin as fallback
            _logger.LogDebug("RCON failed for Docker server {ServerName}, falling back to stdin", server.Name);
        }

        // Try stdin (primary for Native, fallback for Docker)
        try
        {
            var executor = _nodeExecutorFactory.GetExecutor(server);
            await executor.SendCommandAsync(server, command, cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Sent command via stdin to server {ServerName}: {Command}", server.Name, command);
            return Result.Success<string?>(null); // No response for stdin commands
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not running or not managed"))
        {
            // Process not managed (e.g., after manager restart) - try RCON fallback
            _logger.LogDebug("Stdin not available for server {ServerName}, attempting RCON fallback", server.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending command via stdin to server {ServerName}", server.Name);
            // Don't return yet - try RCON fallback for Native servers
        }

        // Try RCON fallback (only for Native servers that haven't tried RCON yet)
        if (!preferRcon && hasRcon)
        {
            var rconResult = await TrySendViaRconAsync(server, command, cancellationToken).ConfigureAwait(false);
            if (rconResult.IsSuccess)
            {
                return rconResult;
            }
        }
        else if (!hasRcon)
        {
            if (_rconService is null)
            {
                _logger.LogWarning("RCON service not available (not injected)");
            }
            else if (server.RconConfig is null)
            {
                _logger.LogWarning("Server {ServerName} has no RCON configuration", server.Name);
            }
            else if (!server.RconConfig.Enabled)
            {
                _logger.LogWarning("RCON is disabled for server {ServerName}", server.Name);
            }
        }

        return Result.Failure<string?>(Error.Rcon("Unable to send command: process not managed and RCON not available. Try restarting the server through the panel."));
    }

    private async Task<Result<string?>> TrySendViaRconAsync(GameServer server, string command, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Attempting RCON connection to {Host}:{Port} for server {ServerName}",
                server.RconConfig!.Host, server.RconConfig.Port, server.Name);

            var rconConfig = new RconConfiguration
            {
                Enabled = server.RconConfig.Enabled,
                Protocol = server.RconConfig.Protocol,
                Host = server.RconConfig.Host,
                Port = server.RconConfig.Port,
                Password = DecryptRconPassword(server.RconConfig.Password ?? ""),
                UseWebSocket = server.RconConfig.UseWebSocket,
                WebRconPath = server.RconConfig.WebRconPath,
                TimeoutSeconds = server.RconConfig.TimeoutSeconds
            };

            var response = await _rconService!.SendCommandAsync(rconConfig, command, cancellationToken).ConfigureAwait(false);

            if (response is not null)
            {
                _logger.LogInformation("Sent command via RCON to server {ServerName}: {Command}, Response: {Response}",
                    server.Name, command, response);
                return Result.Success<string?>(response); // Return the RCON response
            }
            else
            {
                _logger.LogWarning("RCON command returned null for server {ServerName} - connection may have failed", server.Name);
                return Result.Failure<string?>(Error.Rcon("RCON connection failed"));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending command via RCON to server {ServerName}", server.Name);
            return Result.Failure<string?>(Error.Rcon($"Error sending command via RCON: {ex.Message}"));
        }
    }

    public async IAsyncEnumerable<string> StreamConsoleAsync(Guid serverId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var server = await _serverRepository.GetByIdWithGameAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (server is null)
        {
            yield break;
        }

        var executor = _nodeExecutorFactory.GetExecutor(server);

        await foreach (var line in executor.StreamConsoleAsync(server, cancellationToken).ConfigureAwait(false))
        {
            yield return line;
        }
    }

    public async Task<Result> RecreateContainerAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var server = await _serverRepository.GetByIdWithGameAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (server is null)
        {
            return Result.Failure(Error.NotFound("Server", serverId.ToString()));
        }

        if (server.DeploymentType != ServerDeploymentType.Docker)
        {
            return Result.Failure(Error.InvalidOperation("Server is not a Docker deployment"));
        }

        if (server.Status == ServerStatus.Running || server.Status == ServerStatus.Starting)
        {
            return Result.Failure(Error.ServerRunning("recreate container"));
        }

        try
        {
            var executor = _nodeExecutorFactory.GetExecutor(server);

            // Remove the existing container using the cleanup method
            if (!string.IsNullOrEmpty(server.DockerContainerId))
            {
                _logger.LogInformation("Removing container {ContainerId} for server {ServerId}",
                    server.DockerContainerId, server.Id);

                await executor.CleanupAsync(server, cancellationToken).ConfigureAwait(false);
            }

            // Clear the container ID so a new one will be created on next start
            server.DockerContainerId = null;
            await _serverRepository.UpdateAsync(server, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Container removed for server {ServerName}. New container will be created on next start.", server.Name);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recreate container for server {ServerId}", serverId);
            return Result.Failure(Error.Docker($"Failed to recreate container: {ex.Message}"));
        }
    }

    public async Task<Result<List<string>>> UpdateTagsAsync(Guid serverId, List<string> tags, CancellationToken cancellationToken = default)
    {
        try
        {
            var server = await _serverRepository.GetByIdAsync(serverId, cancellationToken).ConfigureAwait(false);
            if (server is null)
                return Result.Failure<List<string>>(Error.NotFound("Server", serverId.ToString()));

            server.Tags = tags.Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).Distinct().ToList();
            await _serverRepository.UpdateAsync(server, cancellationToken).ConfigureAwait(false);

            return Result.Success(server.Tags);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update tags for server {ServerId}", serverId);
            return Result.Failure<List<string>>(Error.FromException(ex));
        }
    }

    public async Task<Result<bool>> TogglePinAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        try
        {
            var server = await _serverRepository.GetByIdAsync(serverId, cancellationToken).ConfigureAwait(false);
            if (server is null)
                return Result.Failure<bool>(Error.NotFound("Server", serverId.ToString()));

            server.IsPinned = !server.IsPinned;
            await _serverRepository.UpdateAsync(server, cancellationToken).ConfigureAwait(false);

            return Result.Success(server.IsPinned);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to toggle pin for server {ServerId}", serverId);
            return Result.Failure<bool>(Error.FromException(ex));
        }
    }

    private GameServerDto MapToDto(GameServer server)
    {
        var rconConfig = server.RconConfig;
        if (rconConfig is not null && !string.IsNullOrEmpty(rconConfig.Password))
        {
            rconConfig = new RconConfiguration
            {
                Enabled = rconConfig.Enabled,
                Protocol = rconConfig.Protocol,
                Host = rconConfig.Host,
                Port = rconConfig.Port,
                Password = DecryptRconPassword(rconConfig.Password),
                UseWebSocket = rconConfig.UseWebSocket,
                WebRconPath = rconConfig.WebRconPath,
                TimeoutSeconds = rconConfig.TimeoutSeconds
            };
        }

        return new GameServerDto(
            server.Id,
            server.Name,
            server.Description,
            server.IconPath,
            server.IconPath ?? server.Game.IconPath,
            server.GameId,
            server.Game.Name,
            server.Game.IconPath,
            server.DeploymentType,
            server.InstallPath,
            server.DockerContainerId,
            server.DockerConfig,
            server.NativeConfig,
            rconConfig,
            server.PrimaryPort,
            server.AdditionalPorts,
            server.BindAddress,
            server.Status,
            server.LastStarted,
            server.LastStopped,
            server.ProcessId,
            server.ResourceLimits,
            server.OwnerId,
            server.Owner.Username,
            server.InstalledModpack,
            server.InstalledVersion,
            server.HytaleInfo,
            server.IsPinned,
            server.Tags,
            server.NodeId,
            server.Node?.Name
        );
    }

    private static GameServerListItemDto MapToListItemDto(GameServer server) => new(
        server.Id,
        server.Name,
        server.Description,
        server.IconPath ?? server.Game.IconPath, // EffectiveIcon: server icon if set, otherwise game icon
        server.GameId,
        server.Game.Name,
        server.DeploymentType,
        server.PrimaryPort,
        server.BindAddress,
        server.Status,
        server.LastStarted,
        server.HytaleInfo?.UpdateAvailable == true && server.HytaleInfo.DismissedVersion != server.HytaleInfo.AvailableVersion,
        server.IsPinned,
        server.Tags,
        server.NodeId,
        server.Node?.Name
    );

    private static DockerConfiguration? MapDockerConfig(DockerConfigurationRequest? request)
    {
        if (request is null) return null;

        return new DockerConfiguration
        {
            Image = request.Image,
            Tag = request.Tag ?? "latest",
            EnvironmentVariables = request.EnvironmentVariables ?? [],
            Volumes = request.Volumes?.Select(v => new VolumeMount
            {
                HostPath = v.HostPath,
                ContainerPath = v.ContainerPath,
                ReadOnly = v.ReadOnly
            }).ToList() ?? [],
            Ports = request.Ports?.Select(p => new PortMapping
            {
                HostPort = p.HostPort,
                ContainerPort = p.ContainerPort,
                Protocol = p.Protocol ?? "tcp"
            }).ToList() ?? [],
            Network = request.Network,
            Limits = MapResourceLimits(request.Limits),
            RestartPolicy = request.RestartPolicy,
            Command = request.Command,
            WorkingDirectory = request.WorkingDirectory,
            RunAsRoot = request.RunAsRoot,
            CommandMode = request.CommandMode ?? DockerCommandMode.Stdin
        };
    }

    private static NativeConfiguration? MapNativeConfig(NativeConfigurationRequest? request)
    {
        if (request is null) return null;

        return new NativeConfiguration
        {
            WorkingDirectory = request.WorkingDirectory,
            ExecutablePath = request.ExecutablePath,
            Arguments = request.Arguments ?? string.Empty,
            EnvironmentVariables = request.EnvironmentVariables ?? [],
            JavaPath = request.JavaPath,
            JavaArguments = request.JavaArguments,
            RunAsService = request.RunAsService,
            RunAsUser = request.RunAsUser,
            UseStartupScript = request.UseStartupScript,
            StartupScriptPath = request.StartupScriptPath
        };
    }

    private static RconConfiguration? MapRconConfig(RconConfigurationRequest? request)
    {
        if (request is null) return null;

        return new RconConfiguration
        {
            Enabled = request.Enabled,
            Protocol = request.Protocol,
            Host = request.Host ?? "localhost",
            Port = request.Port,
            Password = request.Password,
            UseWebSocket = request.UseWebSocket,
            WebRconPath = request.WebRconPath,
            TimeoutSeconds = request.TimeoutSeconds ?? 10
        };
    }

    private static ResourceLimits? MapResourceLimits(ResourceLimitsRequest? request)
    {
        if (request is null) return null;

        return new ResourceLimits
        {
            MaxMemoryMb = request.MaxMemoryMb,
            MaxCpuPercent = request.MaxCpuPercent,
            MaxDiskMb = request.MaxDiskMb,
            MaxNetworkMbps = request.MaxNetworkMbps
        };
    }

    private void EncryptRconPassword(GameServer server)
    {
        if (server.RconConfig is not null && !string.IsNullOrEmpty(server.RconConfig.Password))
        {
            server.RconConfig.Password = _encryptionService.Encrypt(server.RconConfig.Password);
        }
    }

    private string DecryptRconPassword(string encryptedPassword)
    {
        try
        {
            return _encryptionService.Decrypt(encryptedPassword);
        }
        catch
        {
            // If decryption fails, the password may be stored in plain text (pre-migration)
            return encryptedPassword;
        }
    }

    public async Task<Result> InstallServerAsync(
        Guid serverId,
        string? branch = null,
        string? betaPassword = null,
        IProgress<InstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var server = await _serverRepository.GetByIdWithGameAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (server is null)
        {
            return Result.Failure(Error.NotFound("Server", serverId.ToString()));
        }

        if (string.IsNullOrEmpty(server.Game.SteamAppId))
        {
            return Result.Failure(Error.Validation($"Game '{server.Game.Name}' does not have a Steam App ID configured. Manual installation required."));
        }

        if (server.Status != ServerStatus.Stopped && server.Status != ServerStatus.Unknown)
        {
            return Result.Failure(Error.InvalidOperation($"Cannot install server: server is currently {server.Status}. Please stop the server first."));
        }

        _logger.LogInformation("Starting installation of server {ServerName} (App ID: {AppId})", server.Name, server.Game.SteamAppId);

        // Update server status to installing
        server.Status = ServerStatus.Installing;
        await _serverRepository.UpdateAsync(server, cancellationToken).ConfigureAwait(false);

        try
        {
            // Create progress adapter to convert SteamCmdProgress to InstallProgress
            var steamProgress = progress is not null
                ? new Progress<SteamCmdProgress>(p => progress.Report(MapToInstallProgress(p)))
                : null;

            var success = await _steamCmdService.InstallOrUpdateGameAsync(
                server.Game.SteamAppId,
                server.InstallPath,
                branch,
                betaPassword,
                validate: true,
                steamProgress,
                cancellationToken).ConfigureAwait(false);

            // Update server status
            server.Status = ServerStatus.Stopped;
            if (success)
            {
                server.InstalledVersion = await GetInstalledBuildIdAsync(server, cancellationToken).ConfigureAwait(false);
            }
            await _serverRepository.UpdateAsync(server, cancellationToken).ConfigureAwait(false);

            if (!success)
            {
                _logger.LogError("Failed to install server {ServerName}", server.Name);
                return Result.Failure(Error.InstallationFailed($"Installation of server '{server.Name}' failed.", "Check logs for details."));
            }

            _logger.LogInformation("Successfully installed server {ServerName}", server.Name);
            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            server.Status = ServerStatus.Stopped;
            await _serverRepository.UpdateAsync(server, cancellationToken).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error installing server {ServerName}", server.Name);
            server.Status = ServerStatus.Stopped;
            await _serverRepository.UpdateAsync(server, CancellationToken.None).ConfigureAwait(false);
            return Result.Failure(Error.InstallationFailed($"Installation error: {ex.Message}"));
        }
    }

    public async Task<Result> UpdateServerAsync(
        Guid serverId,
        string? branch = null,
        string? betaPassword = null,
        IProgress<UpdateProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var server = await _serverRepository.GetByIdWithGameAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (server is null)
        {
            return Result.Failure(Error.NotFound("Server", serverId.ToString()));
        }

        if (string.IsNullOrEmpty(server.Game.SteamAppId))
        {
            return Result.Failure(Error.Validation($"Game '{server.Game.Name}' does not have a Steam App ID configured. Manual update required."));
        }

        if (server.Status != ServerStatus.Stopped && server.Status != ServerStatus.Unknown)
        {
            return Result.Failure(Error.InvalidOperation($"Cannot update server: server is currently {server.Status}. Please stop the server first."));
        }

        var previousVersion = server.InstalledVersion;
        _logger.LogInformation("Starting update of server {ServerName} (App ID: {AppId})", server.Name, server.Game.SteamAppId);

        // Update server status to updating
        server.Status = ServerStatus.Updating;
        await _serverRepository.UpdateAsync(server, cancellationToken).ConfigureAwait(false);

        try
        {
            // Create progress adapter to convert SteamCmdProgress to UpdateProgress
            var steamProgress = progress is not null
                ? new Progress<SteamCmdProgress>(p => progress.Report(MapToUpdateProgress(p, previousVersion)))
                : null;

            var success = await _steamCmdService.InstallOrUpdateGameAsync(
                server.Game.SteamAppId,
                server.InstallPath,
                branch,
                betaPassword,
                validate: true,
                steamProgress,
                cancellationToken).ConfigureAwait(false);

            // Update server status
            server.Status = ServerStatus.Stopped;
            if (success)
            {
                server.InstalledVersion = await GetInstalledBuildIdAsync(server, cancellationToken).ConfigureAwait(false);
            }
            await _serverRepository.UpdateAsync(server, cancellationToken).ConfigureAwait(false);

            if (!success)
            {
                _logger.LogError("Failed to update server {ServerName}", server.Name);
                return Result.Failure(Error.InstallationFailed($"Update of server '{server.Name}' failed.", "Check logs for details."));
            }

            _logger.LogInformation("Successfully updated server {ServerName} from {OldVersion} to {NewVersion}",
                server.Name, previousVersion, server.InstalledVersion);
            return Result.Success();
        }
        catch (OperationCanceledException)
        {
            server.Status = ServerStatus.Stopped;
            await _serverRepository.UpdateAsync(server, cancellationToken).ConfigureAwait(false);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating server {ServerName}", server.Name);
            server.Status = ServerStatus.Stopped;
            await _serverRepository.UpdateAsync(server, CancellationToken.None).ConfigureAwait(false);
            return Result.Failure(Error.InstallationFailed($"Update error: {ex.Message}"));
        }
    }

    public async Task<Result<bool>> SupportsSteamInstallAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var server = await _serverRepository.GetByIdWithGameAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (server is null)
        {
            return Result.Failure<bool>(Error.NotFound("Server", serverId.ToString()));
        }

        return !string.IsNullOrEmpty(server.Game.SteamAppId);
    }

    public async Task<Result<SteamAppInfo?>> GetSteamAppInfoAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var server = await _serverRepository.GetByIdWithGameAsync(serverId, cancellationToken).ConfigureAwait(false);
        if (server is null)
        {
            return Result.Failure<SteamAppInfo?>(Error.NotFound("Server", serverId.ToString()));
        }

        if (string.IsNullOrEmpty(server.Game.SteamAppId))
        {
            return Result.Success<SteamAppInfo?>(null);
        }

        var appInfo = await _steamCmdService.GetAppInfoAsync(
            server.Game.SteamAppId,
            server.InstallPath,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(appInfo);
    }

    private async Task<string?> GetInstalledBuildIdAsync(GameServer server, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(server.Game.SteamAppId))
        {
            return null;
        }

        var appInfo = await _steamCmdService.GetAppInfoAsync(
            server.Game.SteamAppId,
            server.InstallPath,
            cancellationToken).ConfigureAwait(false);

        return appInfo?.BuildId;
    }

    private static InstallProgress MapToInstallProgress(SteamCmdProgress steamProgress)
    {
        return steamProgress.State switch
        {
            SteamCmdProgressState.Starting or SteamCmdProgressState.Initializing => InstallProgress.Starting(),
            SteamCmdProgressState.DownloadingSteamCmd or SteamCmdProgressState.Extracting => new InstallProgress
            {
                CurrentStep = steamProgress.Message,
                IsIndeterminate = true
            },
            SteamCmdProgressState.LoggingIn => new InstallProgress
            {
                CurrentStep = "Connecting to Steam...",
                IsIndeterminate = true
            },
            SteamCmdProgressState.Validating => new InstallProgress
            {
                CurrentStep = "Validating files...",
                IsIndeterminate = true
            },
            SteamCmdProgressState.DownloadingGame => InstallProgress.Downloading(
                steamProgress.CurrentFile ?? "game files",
                steamProgress.BytesDownloaded,
                steamProgress.TotalBytes),
            SteamCmdProgressState.Installing => InstallProgress.Extracting("game files"),
            SteamCmdProgressState.Completed => InstallProgress.Completed(),
            SteamCmdProgressState.Failed => new InstallProgress
            {
                CurrentStep = steamProgress.Message,
                IsIndeterminate = false
            },
            _ => new InstallProgress
            {
                CurrentStep = steamProgress.Message,
                IsIndeterminate = true
            }
        };
    }

    private static UpdateProgress MapToUpdateProgress(SteamCmdProgress steamProgress, string? fromVersion)
    {
        return steamProgress.State switch
        {
            SteamCmdProgressState.Starting or SteamCmdProgressState.Initializing => UpdateProgress.CheckingForUpdates(),
            SteamCmdProgressState.DownloadingSteamCmd or SteamCmdProgressState.Extracting => new UpdateProgress
            {
                CurrentStep = steamProgress.Message,
                IsIndeterminate = true,
                FromVersion = fromVersion
            },
            SteamCmdProgressState.LoggingIn => new UpdateProgress
            {
                CurrentStep = "Connecting to Steam...",
                IsIndeterminate = true,
                FromVersion = fromVersion
            },
            SteamCmdProgressState.Validating => new UpdateProgress
            {
                CurrentStep = "Validating files...",
                IsIndeterminate = true,
                FromVersion = fromVersion
            },
            SteamCmdProgressState.DownloadingGame => UpdateProgress.Downloading(
                steamProgress.CurrentFile ?? "update files",
                steamProgress.BytesDownloaded,
                steamProgress.TotalBytes),
            SteamCmdProgressState.Installing => UpdateProgress.Applying(),
            SteamCmdProgressState.Completed => UpdateProgress.Completed(fromVersion),
            SteamCmdProgressState.Failed => new UpdateProgress
            {
                CurrentStep = steamProgress.Message,
                IsIndeterminate = false,
                FromVersion = fromVersion
            },
            _ => new UpdateProgress
            {
                CurrentStep = steamProgress.Message,
                IsIndeterminate = true,
                FromVersion = fromVersion
            }
        };
    }
}
