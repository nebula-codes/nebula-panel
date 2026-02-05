namespace NebulaPanel.Application.DTOs;

/// <summary>
/// Summary statistics for the dashboard header cards.
/// </summary>
public record DashboardSummaryDto(
    int TotalServers,
    int RunningServers,
    int StoppedServers,
    int CrashedServers,
    int StartingOrStopping,
    int InstallingOrUpdating
);

/// <summary>
/// A single item in the activity feed.
/// </summary>
public record ActivityFeedItemDto(
    Guid Id,
    Guid? ServerId,
    string? ServerName,
    Guid? UserId,
    string? Username,
    DateTime Timestamp,
    string ActivityType,
    string Category,
    string Description,
    string? IconName,
    string? StatusColor
);

/// <summary>
/// A single data point for resource charts.
/// </summary>
public record ResourceDataPointDto(
    DateTime Timestamp,
    double CpuPercent,
    double MemoryMb,
    int? PlayerCount
);

/// <summary>
/// Resource usage chart data for a single server.
/// </summary>
public record ServerResourceChartDto(
    Guid ServerId,
    string ServerName,
    IReadOnlyList<ResourceDataPointDto> DataPoints
);

/// <summary>
/// System health metrics for the host machine.
/// </summary>
public record SystemHealthDto(
    double CpuPercent,
    double MemoryPercent,
    long MemoryUsedBytes,
    long MemoryTotalBytes,
    IReadOnlyList<DiskUsageDto> Disks,
    DateTime Timestamp
);

/// <summary>
/// Disk usage information for a single drive.
/// </summary>
public record DiskUsageDto(
    string DriveName,
    string DriveLabel,
    long UsedBytes,
    long TotalBytes,
    double UsagePercent
);

/// <summary>
/// Announcement data for display on the dashboard.
/// </summary>
public record AnnouncementDto(
    Guid Id,
    string Title,
    string Content,
    string Type,
    bool IsPinned,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    string CreatedByUsername
);

/// <summary>
/// Request to create a new announcement.
/// </summary>
public record CreateAnnouncementRequest(
    string Title,
    string Content,
    string Type,
    bool IsPinned,
    DateTime? ExpiresAt,
    int DisplayOrder = 0
);

/// <summary>
/// Request to update an existing announcement.
/// </summary>
public record UpdateAnnouncementRequest(
    string Title,
    string Content,
    string Type,
    bool IsActive,
    bool IsPinned,
    DateTime? ExpiresAt,
    int DisplayOrder
);

/// <summary>
/// Combined dashboard data for initial page load.
/// </summary>
public record DashboardDataDto(
    DashboardSummaryDto Summary,
    IReadOnlyList<ActivityFeedItemDto> RecentActivity,
    SystemHealthDto SystemHealth,
    IReadOnlyList<AnnouncementDto> Announcements
);
