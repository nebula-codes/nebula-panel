using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Application.Services;

public interface IAuditService
{
    Task LogEventAsync(
        SecurityEventType eventType,
        Guid? userId,
        string? username,
        bool success,
        string? details = null,
        CancellationToken cancellationToken = default);

    Task LogServerOperationAsync(
        SecurityEventType eventType,
        Guid userId,
        string username,
        Guid serverId,
        string serverName,
        string? details = null,
        CancellationToken cancellationToken = default);

    Task LogPermissionChangeAsync(
        SecurityEventType eventType,
        Guid userId,
        string username,
        string? details = null,
        CancellationToken cancellationToken = default);

    Task LogFileOperationAsync(
        SecurityEventType eventType,
        Guid userId,
        string username,
        Guid serverId,
        string filePath,
        CancellationToken cancellationToken = default);

    Task LogSettingsChangeAsync(
        Guid userId,
        string username,
        string settingArea,
        CancellationToken cancellationToken = default);
}
