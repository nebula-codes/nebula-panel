using Microsoft.AspNetCore.SignalR;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;
using NebulaPanel.Web.Hubs;

namespace NebulaPanel.Web.Services;

/// <summary>
/// Broadcasts Hytale server update progress via SignalR.
/// Uses the InstallProgressHub with a server-specific group.
/// </summary>
public class HytaleUpdateNotifier : IHytaleUpdateNotifier
{
    private readonly IHubContext<InstallProgressHub, IInstallProgressHubClient> _hubContext;
    private readonly ILogger<HytaleUpdateNotifier> _logger;

    public HytaleUpdateNotifier(
        IHubContext<InstallProgressHub, IInstallProgressHubClient> hubContext,
        ILogger<HytaleUpdateNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyProgressAsync(
        Guid serverId,
        HytaleUpdateProgressDto progress,
        CancellationToken ct = default)
    {
        var groupName = GetUpdateGroupName(serverId);

        _logger.LogTrace("Sending Hytale update progress to group {Group}: {Phase} {Percentage}%",
            groupName, progress.Phase, progress.Percentage);

        // Convert to MinecraftInstallProgressDto for hub compatibility
        var installProgress = new MinecraftInstallProgressDto
        {
            InstallationId = progress.ServerId,
            Phase = progress.Phase.ToString(),
            Message = progress.Message,
            Percentage = progress.Percentage,
            BytesDownloaded = progress.BytesDownloaded,
            TotalBytes = progress.TotalBytes,
            Timestamp = progress.Timestamp,
            IsComplete = progress.IsComplete,
            HasError = progress.HasError,
            ErrorMessage = progress.ErrorMessage
        };

        await _hubContext.Clients.Group(groupName)
            .ReceiveProgress(installProgress)
            .ConfigureAwait(false);
    }

    public async Task NotifyCompleteAsync(
        Guid serverId,
        bool success,
        string? newVersion,
        string? error,
        CancellationToken ct = default)
    {
        var groupName = GetUpdateGroupName(serverId);

        _logger.LogDebug("Sending Hytale update complete to group {Group}: Success={Success}, NewVersion={NewVersion}",
            groupName, success, newVersion);

        // Use InstallationComplete with the server ID as the "serverId" parameter
        await _hubContext.Clients.Group(groupName)
            .InstallationComplete(serverId, success, error)
            .ConfigureAwait(false);
    }

    public async Task NotifyUpdateAvailableAsync(
        Guid serverId,
        string currentVersion,
        string availableVersion,
        CancellationToken ct = default)
    {
        var groupName = GetUpdateGroupName(serverId);

        _logger.LogDebug("Notifying update available for server {ServerId}: {CurrentVersion} -> {AvailableVersion}",
            serverId, currentVersion, availableVersion);

        // Send a progress message indicating update availability
        var progress = new MinecraftInstallProgressDto
        {
            InstallationId = serverId,
            Phase = "UpdateAvailable",
            Message = $"Update available: {currentVersion} -> {availableVersion}",
            Percentage = 0,
            Timestamp = DateTime.UtcNow,
            IsComplete = false,
            HasError = false
        };

        await _hubContext.Clients.Group(groupName)
            .ReceiveProgress(progress)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the SignalR group name for a server's update notifications.
    /// </summary>
    public static string GetUpdateGroupName(Guid serverId) => $"hytale-update:{serverId}";
}
