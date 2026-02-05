using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Repositories;

namespace NebulaPanel.Application.Services;

public class ServerActivityService(
    IServerActivityRepository activityRepository) : IServerActivityService
{
    private readonly IServerActivityRepository _activityRepository = activityRepository;

    public async Task LogActivityAsync(
        Guid serverId,
        ServerActivityType activityType,
        string description,
        Guid? userId = null,
        string? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var activity = new ServerActivity
        {
            Id = Guid.NewGuid(),
            ServerId = serverId,
            UserId = userId,
            Timestamp = DateTime.UtcNow,
            ActivityType = activityType,
            Description = description,
            Metadata = metadata
        };

        await _activityRepository.AddAsync(activity, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ActivityFeedItemDto>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        var activities = await _activityRepository.GetRecentAsync(count, cancellationToken).ConfigureAwait(false);
        return activities.Select(MapToDto).ToList();
    }

    public async Task<IReadOnlyList<ActivityFeedItemDto>> GetByServerAsync(Guid serverId, int count, CancellationToken cancellationToken = default)
    {
        var activities = await _activityRepository.GetByServerIdAsync(serverId, count, cancellationToken).ConfigureAwait(false);
        return activities.Select(MapToDto).ToList();
    }

    private static ActivityFeedItemDto MapToDto(ServerActivity activity)
    {
        var (iconName, statusColor) = GetIconAndColor(activity.ActivityType);

        return new ActivityFeedItemDto(
            Id: activity.Id,
            ServerId: activity.ServerId,
            ServerName: activity.Server?.Name,
            UserId: activity.UserId,
            Username: activity.User?.Username,
            Timestamp: activity.Timestamp,
            ActivityType: activity.ActivityType.ToString(),
            Category: "Server",
            Description: activity.Description,
            IconName: iconName,
            StatusColor: statusColor
        );
    }

    private static (string IconName, string StatusColor) GetIconAndColor(ServerActivityType activityType)
    {
        return activityType switch
        {
            ServerActivityType.Started => ("play", "success"),
            ServerActivityType.Stopped => ("square", "info"),
            ServerActivityType.Crashed => ("alert-triangle", "error"),
            ServerActivityType.Restarted => ("refresh-cw", "info"),
            ServerActivityType.Killed => ("x-circle", "warning"),
            ServerActivityType.InstallStarted => ("download", "info"),
            ServerActivityType.InstallCompleted => ("check-circle", "success"),
            ServerActivityType.InstallFailed => ("x-circle", "error"),
            ServerActivityType.UpdateStarted => ("download-cloud", "info"),
            ServerActivityType.UpdateCompleted => ("check-circle", "success"),
            ServerActivityType.UpdateFailed => ("x-circle", "error"),
            ServerActivityType.BackupCreated => ("archive", "success"),
            ServerActivityType.BackupRestored => ("upload", "info"),
            ServerActivityType.ConfigChanged => ("settings", "info"),
            ServerActivityType.ModInstalled => ("package-plus", "success"),
            ServerActivityType.ModRemoved => ("package-minus", "info"),
            ServerActivityType.CommandExecuted => ("terminal", "info"),
            _ => ("activity", "info")
        };
    }
}
