using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Repositories;

namespace NebulaPanel.Application.Services;

public class DashboardService(
    IGameServerRepository serverRepository,
    IServerActivityRepository serverActivityRepository,
    IUserActivityRepository userActivityRepository,
    IResourceUsageHistoryRepository resourceHistoryRepository,
    IAnnouncementRepository announcementRepository,
    IHostMonitorService hostMonitorService,
    IGameServerService gameServerService,
    ILogger<DashboardService> logger) : IDashboardService
{
    private readonly IGameServerRepository _serverRepository = serverRepository;
    private readonly IServerActivityRepository _serverActivityRepository = serverActivityRepository;
    private readonly IUserActivityRepository _userActivityRepository = userActivityRepository;
    private readonly IResourceUsageHistoryRepository _resourceHistoryRepository = resourceHistoryRepository;
    private readonly IAnnouncementRepository _announcementRepository = announcementRepository;
    private readonly IHostMonitorService _hostMonitorService = hostMonitorService;
    private readonly IGameServerService _gameServerService = gameServerService;
    private readonly ILogger<DashboardService> _logger = logger;

    public async Task<DashboardSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var servers = await _serverRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);

        var totalServers = servers.Count;
        var runningServers = servers.Count(s => s.Status == ServerStatus.Running);
        var stoppedServers = servers.Count(s => s.Status == ServerStatus.Stopped || s.Status == ServerStatus.Unknown);
        var crashedServers = servers.Count(s => s.Status == ServerStatus.Crashed);
        var startingOrStopping = servers.Count(s => s.Status == ServerStatus.Starting || s.Status == ServerStatus.Stopping);
        var installingOrUpdating = servers.Count(s => s.Status == ServerStatus.Installing || s.Status == ServerStatus.Updating);

        return new DashboardSummaryDto(
            TotalServers: totalServers,
            RunningServers: runningServers,
            StoppedServers: stoppedServers,
            CrashedServers: crashedServers,
            StartingOrStopping: startingOrStopping,
            InstallingOrUpdating: installingOrUpdating
        );
    }

    public async Task<IReadOnlyList<ActivityFeedItemDto>> GetRecentActivityAsync(int count = 20, CancellationToken cancellationToken = default)
    {
        // Get server activities
        var serverActivities = await _serverActivityRepository.GetRecentAsync(count, cancellationToken).ConfigureAwait(false);
        var serverItems = serverActivities.Select(a => new ActivityFeedItemDto(
            Id: a.Id,
            ServerId: a.ServerId,
            ServerName: a.Server?.Name,
            UserId: a.UserId,
            Username: a.User?.Username,
            Timestamp: a.Timestamp,
            ActivityType: a.ActivityType.ToString(),
            Category: "Server",
            Description: a.Description,
            IconName: GetServerActivityIcon(a.ActivityType),
            StatusColor: GetServerActivityColor(a.ActivityType)
        ));

        // Get user activities (take same count, then merge and sort)
        var userActivities = await _userActivityRepository.GetRecentAsync(count, cancellationToken).ConfigureAwait(false);
        var userItems = userActivities.Select(a => new ActivityFeedItemDto(
            Id: a.Id,
            ServerId: null,
            ServerName: null,
            UserId: a.UserId,
            Username: a.User?.Username,
            Timestamp: a.Timestamp,
            ActivityType: a.ActivityType.ToString(),
            Category: "User",
            Description: a.Description,
            IconName: GetUserActivityIcon(a.ActivityType),
            StatusColor: GetUserActivityColor(a.ActivityType)
        ));

        // Combine, sort by timestamp, take top N
        return serverItems.Concat(userItems)
            .OrderByDescending(a => a.Timestamp)
            .Take(count)
            .ToList();
    }

    public async Task<IReadOnlyList<ServerResourceChartDto>> GetResourceHistoryAsync(TimeSpan period, CancellationToken cancellationToken = default)
    {
        var to = DateTime.UtcNow;
        var from = to - period;

        var allHistory = await _resourceHistoryRepository.GetAllServersAsync(from, to, cancellationToken).ConfigureAwait(false);

        // Group by server
        var grouped = allHistory.GroupBy(r => r.ServerId);

        var result = new List<ServerResourceChartDto>();
        foreach (var group in grouped)
        {
            var serverName = group.First().Server?.Name ?? "Unknown";
            var dataPoints = group.Select(r => new ResourceDataPointDto(
                Timestamp: r.Timestamp,
                CpuPercent: r.CpuPercent,
                MemoryMb: r.MemoryBytes / (1024.0 * 1024.0),
                PlayerCount: r.PlayerCount
            )).ToList();

            result.Add(new ServerResourceChartDto(
                ServerId: group.Key,
                ServerName: serverName,
                DataPoints: dataPoints
            ));
        }

        return result;
    }

    public async Task<SystemHealthDto> GetSystemHealthAsync(CancellationToken cancellationToken = default)
    {
        return await _hostMonitorService.GetCurrentHealthAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<Result> StartAllServersAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var servers = await _serverRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var stoppedServers = servers.Where(s => s.Status == ServerStatus.Stopped || s.Status == ServerStatus.Crashed);

        var errors = new List<string>();
        foreach (var server in stoppedServers)
        {
            try
            {
                var result = await _gameServerService.StartServerAsync(server.Id, cancellationToken).ConfigureAwait(false);
                if (result.IsFailure)
                {
                    errors.Add($"{server.Name}: {result.Error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting server {ServerId}", server.Id);
                errors.Add($"{server.Name}: {ex.Message}");
            }
        }

        if (errors.Count > 0)
        {
            return Result.Failure($"Some servers failed to start: {string.Join("; ", errors)}");
        }

        return Result.Success();
    }

    public async Task<Result> StopAllServersAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var servers = await _serverRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var runningServers = servers.Where(s => s.Status == ServerStatus.Running);

        var errors = new List<string>();
        foreach (var server in runningServers)
        {
            try
            {
                var result = await _gameServerService.StopServerAsync(server.Id, cancellationToken).ConfigureAwait(false);
                if (result.IsFailure)
                {
                    errors.Add($"{server.Name}: {result.Error}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error stopping server {ServerId}", server.Id);
                errors.Add($"{server.Name}: {ex.Message}");
            }
        }

        if (errors.Count > 0)
        {
            return Result.Failure($"Some servers failed to stop: {string.Join("; ", errors)}");
        }

        return Result.Success();
    }

    public async Task<DashboardDataDto> GetDashboardDataAsync(CancellationToken cancellationToken = default)
    {
        // Execute queries in parallel for better performance
        var summaryTask = GetSummaryAsync(cancellationToken);
        var activityTask = GetRecentActivityAsync(10, cancellationToken);
        var healthTask = GetSystemHealthAsync(cancellationToken);
        var announcementsTask = _announcementRepository.GetActiveAsync(cancellationToken);

        await Task.WhenAll(summaryTask, activityTask, healthTask, announcementsTask).ConfigureAwait(false);

        var announcements = (await announcementsTask).Select(a => new AnnouncementDto(
            Id: a.Id,
            Title: a.Title,
            Content: a.Content,
            Type: a.Type.ToString(),
            IsPinned: a.IsPinned,
            CreatedAt: a.CreatedAt,
            ExpiresAt: a.ExpiresAt,
            CreatedByUsername: a.CreatedByUser?.Username ?? "Unknown"
        )).ToList();

        return new DashboardDataDto(
            Summary: await summaryTask,
            RecentActivity: await activityTask,
            SystemHealth: await healthTask,
            Announcements: announcements
        );
    }

    private static string GetServerActivityIcon(ServerActivityType activityType)
    {
        return activityType switch
        {
            ServerActivityType.Started => "play",
            ServerActivityType.Stopped => "square",
            ServerActivityType.Crashed => "alert-triangle",
            ServerActivityType.Restarted => "refresh-cw",
            ServerActivityType.Killed => "x-circle",
            ServerActivityType.InstallStarted or ServerActivityType.InstallCompleted or ServerActivityType.InstallFailed => "download",
            ServerActivityType.UpdateStarted or ServerActivityType.UpdateCompleted or ServerActivityType.UpdateFailed => "download-cloud",
            ServerActivityType.BackupCreated => "archive",
            ServerActivityType.BackupRestored => "upload",
            ServerActivityType.ConfigChanged => "settings",
            ServerActivityType.ModInstalled => "package-plus",
            ServerActivityType.ModRemoved => "package-minus",
            ServerActivityType.CommandExecuted => "terminal",
            _ => "activity"
        };
    }

    private static string GetServerActivityColor(ServerActivityType activityType)
    {
        return activityType switch
        {
            ServerActivityType.Started or ServerActivityType.InstallCompleted or ServerActivityType.UpdateCompleted or ServerActivityType.BackupCreated or ServerActivityType.ModInstalled => "success",
            ServerActivityType.Crashed or ServerActivityType.InstallFailed or ServerActivityType.UpdateFailed => "error",
            ServerActivityType.Killed => "warning",
            _ => "info"
        };
    }

    private static string GetUserActivityIcon(UserActivityType activityType)
    {
        return activityType switch
        {
            UserActivityType.Login => "log-in",
            UserActivityType.Logout => "log-out",
            UserActivityType.FailedLogin => "alert-circle",
            UserActivityType.PasswordChanged or UserActivityType.PasswordResetByAdmin => "key",
            UserActivityType.ProfileUpdated => "user",
            UserActivityType.RoleAssigned or UserActivityType.RoleRemoved => "shield",
            UserActivityType.AccountEnabled or UserActivityType.AccountDisabled => "user-check",
            UserActivityType.AccountCreated or UserActivityType.AccountDeleted => "user-plus",
            _ => "activity"
        };
    }

    private static string GetUserActivityColor(UserActivityType activityType)
    {
        return activityType switch
        {
            UserActivityType.Login or UserActivityType.AccountCreated or UserActivityType.AccountEnabled or UserActivityType.RoleAssigned => "success",
            UserActivityType.FailedLogin or UserActivityType.AccountDeleted or UserActivityType.AccountDisabled => "error",
            UserActivityType.Logout or UserActivityType.RoleRemoved => "warning",
            _ => "info"
        };
    }
}
