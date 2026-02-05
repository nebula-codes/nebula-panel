using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using NebulaPanel.Application.DTOs;

namespace NebulaPanel.Web.Hubs;

/// <summary>
/// SignalR hub client interface for server relocation progress updates.
/// </summary>
public interface IServerRelocationHubClient
{
    /// <summary>
    /// Called when relocation progress is updated.
    /// </summary>
    Task ReceiveProgress(ServerRelocationProgressDto progress);

    /// <summary>
    /// Called when relocation is complete.
    /// </summary>
    Task RelocationComplete(Guid serverId, bool success, string? error);
}

/// <summary>
/// SignalR hub for real-time server relocation progress updates.
/// Requires authentication to connect.
/// </summary>
[Authorize]
public class ServerRelocationHub : Hub<IServerRelocationHubClient>
{
    private readonly ILogger<ServerRelocationHub> _logger;

    public ServerRelocationHub(ILogger<ServerRelocationHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Join the relocation progress group to receive updates.
    /// </summary>
    public async Task JoinRelocation(Guid relocationId)
    {
        var groupName = GetGroupName(relocationId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName).ConfigureAwait(false);
        _logger.LogDebug("Client {ConnectionId} joined relocation group {Group}",
            Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Leave the relocation progress group.
    /// </summary>
    public async Task LeaveRelocation(Guid relocationId)
    {
        var groupName = GetGroupName(relocationId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName).ConfigureAwait(false);
        _logger.LogDebug("Client {ConnectionId} left relocation group {Group}",
            Context.ConnectionId, groupName);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogDebug("Client {ConnectionId} connected to ServerRelocationHub", Context.ConnectionId);
        await base.OnConnectedAsync().ConfigureAwait(false);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception is not null)
        {
            _logger.LogWarning(exception, "Client {ConnectionId} disconnected with error", Context.ConnectionId);
        }
        else
        {
            _logger.LogDebug("Client {ConnectionId} disconnected from ServerRelocationHub", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }

    internal static string GetGroupName(Guid relocationId) => $"relocation:{relocationId}";
}
