namespace NebulaPanel.Infrastructure.Auth;

using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Infrastructure.Persistence;

/// <summary>
/// Service for logging security-related events to the database and Serilog.
/// </summary>
public class SecurityAuditService(
    NebulaPanelDbContext context,
    ILogger<SecurityAuditService> logger) : ISecurityAuditService
{
    public async Task LogEventAsync(
        SecurityEventType eventType,
        Guid? userId,
        string? username,
        string? ipAddress,
        string? userAgent,
        bool success,
        string? details = null,
        CancellationToken ct = default)
    {
        var auditEvent = new SecurityAuditEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Username = username,
            EventType = eventType,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Details = details,
            Success = success,
            OccurredAt = DateTime.UtcNow
        };

        context.SecurityAuditEvents.Add(auditEvent);
        await context.SaveChangesAsync(ct).ConfigureAwait(false);

        // Also log to Serilog with structured data
        if (success)
        {
            logger.LogInformation(
                "Security event: {EventType} for user {Username} from {IpAddress}. Details: {Details}",
                eventType, username ?? "unknown", ipAddress ?? "unknown", details);
        }
        else
        {
            logger.LogWarning(
                "Security event (failed): {EventType} for user {Username} from {IpAddress}. Details: {Details}",
                eventType, username ?? "unknown", ipAddress ?? "unknown", details);
        }
    }

    public Task LogLoginSuccessAsync(Guid userId, string username, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        return LogEventAsync(
            SecurityEventType.LoginSuccess,
            userId,
            username,
            ipAddress,
            userAgent,
            success: true,
            details: null,
            ct);
    }

    public Task LogLoginFailedAsync(string username, string? ipAddress, string? userAgent, string reason, CancellationToken ct = default)
    {
        return LogEventAsync(
            SecurityEventType.LoginFailed,
            userId: null,
            username,
            ipAddress,
            userAgent,
            success: false,
            details: reason,
            ct);
    }

    public Task LogLoginRateLimitedAsync(string username, string? ipAddress, string? userAgent, int retryAfterSeconds, CancellationToken ct = default)
    {
        return LogEventAsync(
            SecurityEventType.LoginFailedRateLimited,
            userId: null,
            username,
            ipAddress,
            userAgent,
            success: false,
            details: $"Rate limited. Retry after {retryAfterSeconds} seconds.",
            ct);
    }

    public Task LogAccountLockedAsync(Guid userId, string username, string? ipAddress, string? userAgent, DateTime lockoutEnd, CancellationToken ct = default)
    {
        return LogEventAsync(
            SecurityEventType.AccountLocked,
            userId,
            username,
            ipAddress,
            userAgent,
            success: false,
            details: $"Account locked until {lockoutEnd:u}",
            ct);
    }

    public Task LogLogoutAsync(Guid userId, string username, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        return LogEventAsync(
            SecurityEventType.Logout,
            userId,
            username,
            ipAddress,
            userAgent,
            success: true,
            details: null,
            ct);
    }

    public Task LogTokenRefreshAsync(Guid userId, string username, string? ipAddress, string? userAgent, bool success, string? details = null, CancellationToken ct = default)
    {
        return LogEventAsync(
            success ? SecurityEventType.TokenRefresh : SecurityEventType.TokenRefreshFailed,
            userId,
            username,
            ipAddress,
            userAgent,
            success,
            details,
            ct);
    }

    public Task LogPasswordChangedAsync(Guid userId, string username, string? ipAddress, string? userAgent, CancellationToken ct = default)
    {
        return LogEventAsync(
            SecurityEventType.PasswordChanged,
            userId,
            username,
            ipAddress,
            userAgent,
            success: true,
            details: null,
            ct);
    }
}
