using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Options;
using NebulaPanel.Agent.Configuration;
using NebulaPanel.Agent.Shared.Mappers;
using NebulaPanel.Agent.Shared.Protos;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.ValueObjects;
using NebulaPanel.Infrastructure.Executors;

namespace NebulaPanel.Agent.Services;

/// <summary>
/// gRPC service implementation that wraps existing executors.
/// Each RPC maps request -> calls executor -> maps response.
/// </summary>
public class AgentGrpcService(
    IServerExecutorFactory executorFactory,
    NativeProcessStateStore nativeStore,
    DockerContainerStateStore dockerStore,
    IOptions<AgentOptions> agentOptions,
    ILogger<AgentGrpcService> logger) : AgentService.AgentServiceBase
{
    private readonly DateTime _startedAt = DateTime.UtcNow;

    // ─── Heartbeat ────────────────────────────────────────────

    public override Task<HeartbeatResponse> Heartbeat(HeartbeatRequest request, ServerCallContext context)
    {
        ValidateToken(context);

        var response = new HeartbeatResponse
        {
            UptimeSeconds = (long)(DateTime.UtcNow - _startedAt).TotalSeconds,
        };

        // Report all running native processes
        foreach (var serverId in nativeStore.GetAllServerIds())
        {
            response.RunningServers.Add(new RunningServerInfo
            {
                ServerId = serverId.ToString(),
                DeploymentType = Shared.Protos.ServerDeploymentType.Native,
                Status = Shared.Protos.ServerStatus.Running,
            });
        }

        // Report all running Docker containers
        foreach (var serverId in dockerStore.GetAllServerIds())
        {
            response.RunningServers.Add(new RunningServerInfo
            {
                ServerId = serverId.ToString(),
                DeploymentType = Shared.Protos.ServerDeploymentType.Docker,
                Status = Shared.Protos.ServerStatus.Running,
            });
        }

        return Task.FromResult(response);
    }

    // ─── Start ────────────────────────────────────────────────

    public override async Task<ServerOperationResponse> StartServer(StartServerRequest request, ServerCallContext context)
    {
        ValidateToken(context);

        try
        {
            var server = ServerConfigMapper.ToGameServer(request.Config);

            // Inject Hytale tokens if provided
            InjectHytaleTokens(server, request);

            var executor = executorFactory.GetExecutor(server.DeploymentType);
            var success = await executor.StartAsync(server, context.CancellationToken).ConfigureAwait(false);

            return new ServerOperationResponse { Success = success };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to start server {ServerId}", request.Config?.ServerId);
            return new ServerOperationResponse { Success = false, Error = ex.Message };
        }
    }

    // ─── Stop ─────────────────────────────────────────────────

    public override async Task<ServerOperationResponse> StopServer(StopServerRequest request, ServerCallContext context)
    {
        ValidateToken(context);

        try
        {
            var server = BuildMinimalServer(request.ServerId,
                ServerConfigMapper.ToDomainDeploymentType(request.DeploymentType),
                request.DockerContainerId,
                request.ProcessId);

            var executor = executorFactory.GetExecutor(server.DeploymentType);
            var success = await executor.StopAsync(server, request.Force, context.CancellationToken).ConfigureAwait(false);

            return new ServerOperationResponse { Success = success };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to stop server {ServerId}", request.ServerId);
            return new ServerOperationResponse { Success = false, Error = ex.Message };
        }
    }

    // ─── Restart ──────────────────────────────────────────────

    public override async Task<ServerOperationResponse> RestartServer(RestartServerRequest request, ServerCallContext context)
    {
        ValidateToken(context);

        try
        {
            var server = ServerConfigMapper.ToGameServer(request.Config);
            var executor = executorFactory.GetExecutor(server.DeploymentType);
            var success = await executor.RestartAsync(server, context.CancellationToken).ConfigureAwait(false);

            return new ServerOperationResponse { Success = success };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to restart server {ServerId}", request.Config?.ServerId);
            return new ServerOperationResponse { Success = false, Error = ex.Message };
        }
    }

    // ─── GetServerStatus ──────────────────────────────────────

    public override async Task<GetServerStatusResponse> GetServerStatus(GetServerStatusRequest request, ServerCallContext context)
    {
        ValidateToken(context);

        var server = BuildMinimalServer(request.ServerId,
            ServerConfigMapper.ToDomainDeploymentType(request.DeploymentType),
            request.DockerContainerId,
            request.ProcessId);

        var executor = executorFactory.GetExecutor(server.DeploymentType);
        var status = await executor.GetStatusAsync(server, context.CancellationToken).ConfigureAwait(false);

        return new GetServerStatusResponse
        {
            Status = ServerConfigMapper.ToProtoStatus(status),
        };
    }

    // ─── SendCommand ──────────────────────────────────────────

    public override async Task<SendCommandResponse> SendCommand(SendCommandRequest request, ServerCallContext context)
    {
        ValidateToken(context);

        try
        {
            var deploymentType = ServerConfigMapper.ToDomainDeploymentType(request.DeploymentType);
            var server = BuildMinimalServer(request.ServerId, deploymentType);

            var executor = executorFactory.GetExecutor(deploymentType);
            await executor.SendCommandAsync(server, request.Command, context.CancellationToken).ConfigureAwait(false);

            return new SendCommandResponse { Success = true };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send command to server {ServerId}", request.ServerId);
            return new SendCommandResponse { Success = false, Error = ex.Message };
        }
    }

    // ─── Cleanup ──────────────────────────────────────────────

    public override async Task<ServerOperationResponse> CleanupServer(CleanupServerRequest request, ServerCallContext context)
    {
        ValidateToken(context);

        try
        {
            var server = BuildMinimalServer(request.ServerId,
                ServerConfigMapper.ToDomainDeploymentType(request.DeploymentType),
                request.DockerContainerId);

            var executor = executorFactory.GetExecutor(server.DeploymentType);
            await executor.CleanupAsync(server, context.CancellationToken).ConfigureAwait(false);

            return new ServerOperationResponse { Success = true };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to cleanup server {ServerId}", request.ServerId);
            return new ServerOperationResponse { Success = false, Error = ex.Message };
        }
    }

    // ─── StreamConsole ────────────────────────────────────────

    public override async Task StreamConsole(
        StreamConsoleRequest request,
        IServerStreamWriter<ConsoleOutputLine> responseStream,
        ServerCallContext context)
    {
        ValidateToken(context);

        var deploymentType = ServerConfigMapper.ToDomainDeploymentType(request.DeploymentType);
        var server = BuildMinimalServer(request.ServerId, deploymentType);

        var executor = executorFactory.GetExecutor(deploymentType);

        await foreach (var line in executor.StreamConsoleAsync(server, context.CancellationToken).ConfigureAwait(false))
        {
            await responseStream.WriteAsync(new ConsoleOutputLine
            {
                Line = line,
                Timestamp = Timestamp.FromDateTime(DateTime.UtcNow),
            }, context.CancellationToken).ConfigureAwait(false);
        }
    }

    // ─── InstallServer ────────────────────────────────────────

    public override async Task InstallServer(
        InstallServerRequest request,
        IServerStreamWriter<InstallProgressUpdate> responseStream,
        ServerCallContext context)
    {
        ValidateToken(context);

        var server = ServerConfigMapper.ToGameServer(request.Config);
        var executor = executorFactory.GetExecutor(server.DeploymentType);

        var progress = new Progress<InstallProgress>(async p =>
        {
            try
            {
                await responseStream.WriteAsync(new InstallProgressUpdate
                {
                    PercentComplete = p.PercentComplete,
                    CurrentStep = p.CurrentStep,
                    CurrentFile = p.CurrentFile,
                    BytesDownloaded = p.BytesDownloaded,
                    TotalBytes = p.TotalBytes,
                    IsIndeterminate = p.IsIndeterminate,
                }, context.CancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to write install progress");
            }
        });

        var success = await executor.InstallAsync(server, progress, context.CancellationToken).ConfigureAwait(false);

        // Send completion message
        await responseStream.WriteAsync(new InstallProgressUpdate
        {
            Completed = true,
            Success = success,
            PercentComplete = success ? 100 : 0,
            CurrentStep = success ? "Installation complete" : "Installation failed",
        }, context.CancellationToken).ConfigureAwait(false);
    }

    // ─── UpdateServer ─────────────────────────────────────────

    public override async Task UpdateServer(
        UpdateServerRequest request,
        IServerStreamWriter<UpdateProgressUpdate> responseStream,
        ServerCallContext context)
    {
        ValidateToken(context);

        var server = ServerConfigMapper.ToGameServer(request.Config);
        var executor = executorFactory.GetExecutor(server.DeploymentType);

        var progress = new Progress<UpdateProgress>(async p =>
        {
            try
            {
                await responseStream.WriteAsync(new UpdateProgressUpdate
                {
                    PercentComplete = p.PercentComplete,
                    CurrentStep = p.CurrentStep,
                    CurrentFile = p.CurrentFile,
                    BytesDownloaded = p.BytesDownloaded,
                    TotalBytes = p.TotalBytes,
                    IsIndeterminate = p.IsIndeterminate,
                    FromVersion = p.FromVersion,
                    ToVersion = p.ToVersion,
                }, context.CancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to write update progress");
            }
        });

        var success = await executor.UpdateAsync(server, progress, context.CancellationToken).ConfigureAwait(false);

        await responseStream.WriteAsync(new UpdateProgressUpdate
        {
            Completed = true,
            Success = success,
            PercentComplete = success ? 100 : 0,
            CurrentStep = success ? "Update complete" : "Update failed",
        }, context.CancellationToken).ConfigureAwait(false);
    }

    // ─── GetResourceUsage ─────────────────────────────────────

    public override async Task<ResourceUsageResponse> GetResourceUsage(GetResourceUsageRequest request, ServerCallContext context)
    {
        ValidateToken(context);

        var deploymentType = ServerConfigMapper.ToDomainDeploymentType(request.DeploymentType);
        var server = BuildMinimalServer(request.ServerId, deploymentType);

        var executor = executorFactory.GetExecutor(deploymentType);
        var usage = await executor.GetResourceUsageAsync(server, context.CancellationToken).ConfigureAwait(false);

        return ServerConfigMapper.FromDomainResourceUsage(usage);
    }

    // ─── Helpers ──────────────────────────────────────────────

    private void ValidateToken(ServerCallContext context)
    {
        var expectedToken = agentOptions.Value.Token;
        if (string.IsNullOrEmpty(expectedToken))
        {
            logger.LogWarning("Agent token is not configured — rejecting request");
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Agent token not configured"));
        }

        var authHeader = context.RequestHeaders.GetValue("authorization");
        if (string.IsNullOrEmpty(authHeader) ||
            !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Missing or invalid authorization"));
        }

        var token = authHeader["Bearer ".Length..];
        if (!string.Equals(token, expectedToken, StringComparison.Ordinal))
        {
            logger.LogWarning("Invalid agent token from {Peer}", context.Peer);
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid token"));
        }
    }

    /// <summary>
    /// Builds a minimal GameServer with just the fields needed for identity-based operations
    /// (stop, status, send command, cleanup, resource usage, stream console).
    /// </summary>
    private static Domain.Entities.GameServer BuildMinimalServer(
        string serverId,
        Domain.Enums.ServerDeploymentType deploymentType,
        string? dockerContainerId = null,
        int? processId = null)
    {
        return new Domain.Entities.GameServer
        {
            Id = Guid.Parse(serverId),
            Name = string.Empty,
            DeploymentType = deploymentType,
            DockerContainerId = dockerContainerId,
            ProcessId = processId,
            OwnerId = Guid.Empty,
        };
    }

    /// <summary>
    /// Injects Hytale session/identity tokens into server config before starting.
    /// Manager resolves tokens via IHytaleSessionManager, passes them in the RPC.
    /// Agent injects into NativeConfig.Arguments or DockerConfig env vars.
    /// </summary>
    private static void InjectHytaleTokens(
        Domain.Entities.GameServer server,
        StartServerRequest request)
    {
        var sessionToken = request.HytaleSessionToken;
        var identityToken = request.HytaleIdentityToken;

        if (string.IsNullOrEmpty(sessionToken) || string.IsNullOrEmpty(identityToken))
            return;

        if (server.DeploymentType == Domain.Enums.ServerDeploymentType.Native)
        {
            // Inject as CLI arguments for native process
            server.NativeConfig ??= new NativeConfiguration();
            var args = server.NativeConfig.Arguments ?? string.Empty;
            if (!args.Contains("--hytale-session-token"))
            {
                server.NativeConfig.Arguments =
                    $"{args} --hytale-session-token {sessionToken} --hytale-identity-token {identityToken}".Trim();
            }
        }
        else if (server.DeploymentType == Domain.Enums.ServerDeploymentType.Docker)
        {
            // Inject as environment variables for Docker container
            server.DockerConfig ??= new DockerConfiguration();
            server.DockerConfig.EnvironmentVariables["HYTALE_SESSION_TOKEN"] = sessionToken;
            server.DockerConfig.EnvironmentVariables["HYTALE_IDENTITY_TOKEN"] = identityToken;
        }
    }
}
