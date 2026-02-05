using Microsoft.AspNetCore.SignalR;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;
using NebulaPanel.Web.Hubs;

namespace NebulaPanel.Web.Services;

/// <summary>
/// Broadcasts server relocation progress via SignalR.
/// </summary>
public class ServerRelocationNotifier : IServerRelocationNotifier
{
    private readonly IHubContext<ServerRelocationHub, IServerRelocationHubClient> _hubContext;
    private readonly ILogger<ServerRelocationNotifier> _logger;

    public ServerRelocationNotifier(
        IHubContext<ServerRelocationHub, IServerRelocationHubClient> hubContext,
        ILogger<ServerRelocationNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyProgressAsync(
        Guid relocationId,
        Guid serverId,
        ServerRelocationProgressDto progress,
        CancellationToken cancellationToken = default)
    {
        var groupName = ServerRelocationHub.GetGroupName(relocationId);

        _logger.LogTrace("Sending relocation progress update to group {Group}: {State} {Percentage}%",
            groupName, progress.State, progress.PercentComplete);

        await _hubContext.Clients.Group(groupName)
            .ReceiveProgress(progress)
            .ConfigureAwait(false);
    }

    public async Task NotifyCompleteAsync(
        Guid relocationId,
        Guid serverId,
        bool success,
        string? error,
        CancellationToken cancellationToken = default)
    {
        var groupName = ServerRelocationHub.GetGroupName(relocationId);

        _logger.LogDebug("Sending relocation complete to group {Group}: Success={Success}, ServerId={ServerId}",
            groupName, success, serverId);

        await _hubContext.Clients.Group(groupName)
            .RelocationComplete(serverId, success, error)
            .ConfigureAwait(false);
    }
}
