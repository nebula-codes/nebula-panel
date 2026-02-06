using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Infrastructure.Persistence;

namespace NebulaPanel.Infrastructure.Auth;

public class AuditService(
    NebulaPanelDbContext context,
    ILogger<AuditService> logger) : IAuditService
{
    public async Task LogEventAsync(
        SecurityEventType eventType,
        Guid? userId,
        string? username,
        bool success,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        var auditEvent = new SecurityAuditEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Username = username,
            EventType = eventType,
            Success = success,
            Details = details,
            OccurredAt = DateTime.UtcNow
        };

        context.SecurityAuditEvents.Add(auditEvent);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        logger.LogInformation(
            "Audit: {EventType} by {Username} (success={Success}). {Details}",
            eventType, username ?? "system", success, details);
    }

    public Task LogServerOperationAsync(
        SecurityEventType eventType,
        Guid userId,
        string username,
        Guid serverId,
        string serverName,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        return LogEventAsync(
            eventType,
            userId,
            username,
            true,
            $"Server: {serverName} ({serverId}). {details}",
            cancellationToken);
    }

    public Task LogPermissionChangeAsync(
        SecurityEventType eventType,
        Guid userId,
        string username,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        return LogEventAsync(eventType, userId, username, true, details, cancellationToken);
    }

    public Task LogFileOperationAsync(
        SecurityEventType eventType,
        Guid userId,
        string username,
        Guid serverId,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        return LogEventAsync(
            eventType,
            userId,
            username,
            true,
            $"Server: {serverId}, File: {filePath}",
            cancellationToken);
    }

    public Task LogSettingsChangeAsync(
        Guid userId,
        string username,
        string settingArea,
        CancellationToken cancellationToken = default)
    {
        return LogEventAsync(
            SecurityEventType.SettingsChanged,
            userId,
            username,
            true,
            $"Settings area: {settingArea}",
            cancellationToken);
    }
}
