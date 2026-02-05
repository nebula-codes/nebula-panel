using Microsoft.AspNetCore.SignalR;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;
using NebulaPanel.Web.Hubs;

namespace NebulaPanel.Web.Services;

/// <summary>
/// Broadcasts Minecraft installation progress via SignalR.
/// </summary>
public class MinecraftInstallNotifier : IMinecraftInstallNotifier
{
    private readonly IHubContext<InstallProgressHub, IInstallProgressHubClient> _hubContext;
    private readonly ILogger<MinecraftInstallNotifier> _logger;

    public MinecraftInstallNotifier(
        IHubContext<InstallProgressHub, IInstallProgressHubClient> hubContext,
        ILogger<MinecraftInstallNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyProgressAsync(
        Guid installationId,
        MinecraftInstallProgressDto progress,
        CancellationToken cancellationToken = default)
    {
        var groupName = InstallProgressHub.GetGroupName(installationId);

        _logger.LogTrace("Sending progress update to group {Group}: {Phase} {Percentage}%",
            groupName, progress.Phase, progress.Percentage);

        await _hubContext.Clients.Group(groupName)
            .ReceiveProgress(progress)
            .ConfigureAwait(false);
    }

    public async Task NotifyCompleteAsync(
        Guid installationId,
        Guid serverId,
        bool success,
        string? error,
        CancellationToken cancellationToken = default)
    {
        var groupName = InstallProgressHub.GetGroupName(installationId);

        _logger.LogDebug("Sending installation complete to group {Group}: Success={Success}, ServerId={ServerId}",
            groupName, success, serverId);

        await _hubContext.Clients.Group(groupName)
            .InstallationComplete(serverId, success, error)
            .ConfigureAwait(false);
    }

    public async Task NotifyLogLineAsync(
        Guid installationId,
        string line,
        CancellationToken cancellationToken = default)
    {
        var groupName = InstallProgressHub.GetGroupName(installationId);

        await _hubContext.Clients.Group(groupName)
            .ReceiveLogLine(installationId, line)
            .ConfigureAwait(false);
    }
}
