using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using NebulaPanel.Application.DTOs;

namespace NebulaPanel.Web.Hubs;

/// <summary>
/// SignalR hub client interface for installation progress updates.
/// </summary>
public interface IInstallProgressHubClient
{
    /// <summary>
    /// Called when installation progress is updated.
    /// </summary>
    Task ReceiveProgress(MinecraftInstallProgressDto progress);

    /// <summary>
    /// Called when installation is complete.
    /// </summary>
    Task InstallationComplete(Guid serverId, bool success, string? error);

    /// <summary>
    /// Called when a new log line is available.
    /// </summary>
    Task ReceiveLogLine(Guid installationId, string line);
}

/// <summary>
/// SignalR hub for real-time installation progress updates.
/// Requires authentication to connect.
/// </summary>
[Authorize]
public class InstallProgressHub : Hub<IInstallProgressHubClient>
{
    private readonly ILogger<InstallProgressHub> _logger;

    public InstallProgressHub(ILogger<InstallProgressHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Join the installation progress group to receive updates.
    /// </summary>
    public async Task JoinInstallation(Guid installationId)
    {
        var groupName = GetGroupName(installationId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName).ConfigureAwait(false);
        _logger.LogDebug("Client {ConnectionId} joined installation group {Group}",
            Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Leave the installation progress group.
    /// </summary>
    public async Task LeaveInstallation(Guid installationId)
    {
        var groupName = GetGroupName(installationId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName).ConfigureAwait(false);
        _logger.LogDebug("Client {ConnectionId} left installation group {Group}",
            Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Join the Hytale update progress group to receive updates.
    /// </summary>
    public async Task JoinHytaleUpdate(Guid serverId)
    {
        var groupName = GetHytaleUpdateGroupName(serverId);
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName).ConfigureAwait(false);
        _logger.LogDebug("Client {ConnectionId} joined Hytale update group {Group}",
            Context.ConnectionId, groupName);
    }

    /// <summary>
    /// Leave the Hytale update progress group.
    /// </summary>
    public async Task LeaveHytaleUpdate(Guid serverId)
    {
        var groupName = GetHytaleUpdateGroupName(serverId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, groupName).ConfigureAwait(false);
        _logger.LogDebug("Client {ConnectionId} left Hytale update group {Group}",
            Context.ConnectionId, groupName);
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogDebug("Client {ConnectionId} connected to InstallProgressHub", Context.ConnectionId);
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
            _logger.LogDebug("Client {ConnectionId} disconnected from InstallProgressHub", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }

    internal static string GetGroupName(Guid installationId) => $"install:{installationId}";

    internal static string GetHytaleUpdateGroupName(Guid serverId) => $"hytale-update:{serverId}";
}
