using System.Runtime.CompilerServices;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using NebulaPanel.Agent.Shared.Mappers;
using NebulaPanel.Agent.Shared.Protos;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.ValueObjects;
using ServerDeploymentType = NebulaPanel.Domain.Enums.ServerDeploymentType;
using ServerStatus = NebulaPanel.Domain.Enums.ServerStatus;

namespace NebulaPanel.Infrastructure.Executors;

/// <summary>
/// IServerExecutor implementation that delegates operations to a remote Agent via gRPC.
/// One instance per call — wraps AgentService.AgentServiceClient.
/// </summary>
public class RemoteServerExecutor : IServerExecutor
{
    private readonly AgentService.AgentServiceClient _client;
    private readonly CallOptions _callOptions;
    private readonly ILogger _logger;

    public RemoteServerExecutor(
        AgentService.AgentServiceClient client,
        CallOptions callOptions,
        ILogger logger)
    {
        _client = client;
        _callOptions = callOptions;
        _logger = logger;
    }

    public ServerDeploymentType DeploymentType =>
        throw new NotSupportedException("RemoteServerExecutor handles all deployment types via gRPC.");

    public async Task<bool> InstallAsync(
        GameServer server,
        IProgress<InstallProgress>? progress = null,
        CancellationToken ct = default)
    {
        var request = new InstallServerRequest { Config = ServerConfigMapper.FromGameServer(server) };
        var options = _callOptions.WithCancellationToken(ct);

        using var call = _client.InstallServer(request, options);

        bool success = false;
        await foreach (var update in call.ResponseStream.ReadAllAsync(ct).ConfigureAwait(false))
        {
            if (update.Completed)
            {
                success = update.Success;
                if (!success)
                {
                    _logger.LogError("Remote install failed for server {ServerId}: {Error}",
                        server.Id, update.Error);
                }
                break;
            }

            progress?.Report(new InstallProgress
            {
                PercentComplete = update.PercentComplete,
                CurrentStep = update.CurrentStep,
                CurrentFile = update.CurrentFile,
                BytesDownloaded = update.BytesDownloaded,
                TotalBytes = update.TotalBytes,
                IsIndeterminate = update.IsIndeterminate
            });
        }

        return success;
    }

    public async Task<bool> UpdateAsync(
        GameServer server,
        IProgress<UpdateProgress>? progress = null,
        CancellationToken ct = default)
    {
        var request = new UpdateServerRequest { Config = ServerConfigMapper.FromGameServer(server) };
        var options = _callOptions.WithCancellationToken(ct);

        using var call = _client.UpdateServer(request, options);

        bool success = false;
        await foreach (var update in call.ResponseStream.ReadAllAsync(ct).ConfigureAwait(false))
        {
            if (update.Completed)
            {
                success = update.Success;
                if (!success)
                {
                    _logger.LogError("Remote update failed for server {ServerId}: {Error}",
                        server.Id, update.Error);
                }
                break;
            }

            progress?.Report(new UpdateProgress
            {
                PercentComplete = update.PercentComplete,
                CurrentStep = update.CurrentStep,
                CurrentFile = update.CurrentFile,
                BytesDownloaded = update.BytesDownloaded,
                TotalBytes = update.TotalBytes,
                IsIndeterminate = update.IsIndeterminate,
                FromVersion = update.FromVersion,
                ToVersion = update.ToVersion
            });
        }

        return success;
    }

    public async Task<bool> StartAsync(GameServer server, CancellationToken ct = default)
    {
        var request = new StartServerRequest { Config = ServerConfigMapper.FromGameServer(server) };
        var options = _callOptions.WithCancellationToken(ct);

        var response = await _client.StartServerAsync(request, options).ConfigureAwait(false);

        if (!response.Success)
        {
            _logger.LogError("Remote start failed for server {ServerId}: {Error}",
                server.Id, response.Error);
        }

        return response.Success;
    }

    public async Task<bool> StopAsync(GameServer server, bool force = false, CancellationToken ct = default)
    {
        var request = new StopServerRequest
        {
            ServerId = server.Id.ToString(),
            DeploymentType = ServerConfigMapper.ToProtoDeploymentType(server.DeploymentType),
            DockerContainerId = server.DockerContainerId,
            ProcessId = server.ProcessId,
            Force = force
        };
        var options = _callOptions.WithCancellationToken(ct);

        var response = await _client.StopServerAsync(request, options).ConfigureAwait(false);

        if (!response.Success)
        {
            _logger.LogError("Remote stop failed for server {ServerId}: {Error}",
                server.Id, response.Error);
        }

        return response.Success;
    }

    public async Task<bool> RestartAsync(GameServer server, CancellationToken ct = default)
    {
        var request = new RestartServerRequest { Config = ServerConfigMapper.FromGameServer(server) };
        var options = _callOptions.WithCancellationToken(ct);

        var response = await _client.RestartServerAsync(request, options).ConfigureAwait(false);

        if (!response.Success)
        {
            _logger.LogError("Remote restart failed for server {ServerId}: {Error}",
                server.Id, response.Error);
        }

        return response.Success;
    }

    public async Task<ServerStatus> GetStatusAsync(GameServer server, CancellationToken ct = default)
    {
        var request = new GetServerStatusRequest
        {
            ServerId = server.Id.ToString(),
            DeploymentType = ServerConfigMapper.ToProtoDeploymentType(server.DeploymentType),
            DockerContainerId = server.DockerContainerId,
            ProcessId = server.ProcessId
        };
        var options = _callOptions.WithCancellationToken(ct);

        var response = await _client.GetServerStatusAsync(request, options).ConfigureAwait(false);

        return ServerConfigMapper.ToDomainStatus(response.Status);
    }

    public async IAsyncEnumerable<string> StreamConsoleAsync(
        GameServer server,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var request = new StreamConsoleRequest
        {
            ServerId = server.Id.ToString(),
            DeploymentType = ServerConfigMapper.ToProtoDeploymentType(server.DeploymentType)
        };
        var options = _callOptions.WithCancellationToken(ct);

        using var call = _client.StreamConsole(request, options);

        await foreach (var line in call.ResponseStream.ReadAllAsync(ct).ConfigureAwait(false))
        {
            yield return line.Line;
        }
    }

    public async Task SendCommandAsync(GameServer server, string command, CancellationToken ct = default)
    {
        var request = new SendCommandRequest
        {
            ServerId = server.Id.ToString(),
            DeploymentType = ServerConfigMapper.ToProtoDeploymentType(server.DeploymentType),
            Command = command
        };
        var options = _callOptions.WithCancellationToken(ct);

        var response = await _client.SendCommandAsync(request, options).ConfigureAwait(false);

        if (!response.Success)
        {
            throw new InvalidOperationException(
                $"Remote command failed for server {server.Id}: {response.Error}");
        }
    }

    public async Task<ResourceUsage> GetResourceUsageAsync(GameServer server, CancellationToken ct = default)
    {
        var request = new GetResourceUsageRequest
        {
            ServerId = server.Id.ToString(),
            DeploymentType = ServerConfigMapper.ToProtoDeploymentType(server.DeploymentType)
        };
        var options = _callOptions.WithCancellationToken(ct);

        var response = await _client.GetResourceUsageAsync(request, options).ConfigureAwait(false);

        return ServerConfigMapper.ToDomainResourceUsage(response);
    }

    public async Task CleanupAsync(GameServer server, CancellationToken ct = default)
    {
        var request = new CleanupServerRequest
        {
            ServerId = server.Id.ToString(),
            DeploymentType = ServerConfigMapper.ToProtoDeploymentType(server.DeploymentType),
            DockerContainerId = server.DockerContainerId
        };
        var options = _callOptions.WithCancellationToken(ct);

        var response = await _client.CleanupServerAsync(request, options).ConfigureAwait(false);

        if (!response.Success)
        {
            _logger.LogError("Remote cleanup failed for server {ServerId}: {Error}",
                server.Id, response.Error);
        }
    }
}
