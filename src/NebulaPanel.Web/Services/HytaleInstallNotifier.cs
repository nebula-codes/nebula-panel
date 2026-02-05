using Microsoft.AspNetCore.SignalR;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Infrastructure.OfficialGames.Hytale;
using NebulaPanel.Web.Hubs;

namespace NebulaPanel.Web.Services;

/// <summary>
/// Broadcasts Hytale installation progress via SignalR.
/// Converts HytaleInstallProgressDto to MinecraftInstallProgressDto for hub compatibility.
/// </summary>
public class HytaleInstallNotifier : IHytaleInstallNotifier
{
    private readonly IHubContext<InstallProgressHub, IInstallProgressHubClient> _hubContext;
    private readonly ILogger<HytaleInstallNotifier> _logger;

    public HytaleInstallNotifier(
        IHubContext<InstallProgressHub, IInstallProgressHubClient> hubContext,
        ILogger<HytaleInstallNotifier> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyProgressAsync(
        Guid installationId,
        HytaleInstallProgressDto progress,
        CancellationToken ct = default)
    {
        var groupName = InstallProgressHub.GetGroupName(installationId);

        _logger.LogInformation("Sending Hytale progress update to group {Group}: {Phase} - {Message} ({Percentage}%)",
            groupName, progress.Phase, progress.Message, progress.Percentage);

        // Convert to MinecraftInstallProgressDto for hub compatibility
        var minecraftProgress = new MinecraftInstallProgressDto
        {
            InstallationId = progress.InstallationId,
            Phase = progress.Phase,
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
            .ReceiveProgress(minecraftProgress)
            .ConfigureAwait(false);
    }

    public async Task NotifyCompleteAsync(
        Guid installationId,
        Guid serverId,
        bool success,
        string? error,
        CancellationToken ct = default)
    {
        var groupName = InstallProgressHub.GetGroupName(installationId);

        _logger.LogDebug("Sending Hytale installation complete to group {Group}: Success={Success}, ServerId={ServerId}",
            groupName, success, serverId);

        await _hubContext.Clients.Group(groupName)
            .InstallationComplete(serverId, success, error)
            .ConfigureAwait(false);
    }

    public async Task NotifyLogLineAsync(
        Guid installationId,
        string line,
        CancellationToken ct = default)
    {
        var groupName = InstallProgressHub.GetGroupName(installationId);

        await _hubContext.Clients.Group(groupName)
            .ReceiveLogLine(installationId, line)
            .ConfigureAwait(false);
    }
}
