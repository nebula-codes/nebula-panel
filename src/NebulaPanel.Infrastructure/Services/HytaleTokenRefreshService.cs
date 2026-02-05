namespace NebulaPanel.Infrastructure.Services;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Repositories;

/// <summary>
/// Background service that periodically checks for and refreshes expiring Hytale tokens.
/// </summary>
public class HytaleTokenRefreshService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<HytaleTokenRefreshService> _logger;

    /// <summary>
    /// How often to check for tokens that need refreshing.
    /// </summary>
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Refresh tokens that expire within this window.
    /// </summary>
    private readonly TimeSpan _refreshWindow;

    public HytaleTokenRefreshService(
        IServiceProvider services,
        IOptions<HytaleAuthSettings> settings,
        ILogger<HytaleTokenRefreshService> logger)
    {
        _services = services;
        _logger = logger;

        // Use setting if available, otherwise default to 1 hour
        _refreshWindow = settings.Value.RefreshBeforeExpiration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Hytale token refresh service started. Check interval: {CheckInterval}, Refresh window: {RefreshWindow}",
            _checkInterval,
            _refreshWindow);

        // Wait a bit before first check to let app fully start
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshExpiringTokensAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Hytale token refresh service");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Hytale token refresh service stopped");
    }

    private async Task RefreshExpiringTokensAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var credentialsRepo = scope.ServiceProvider.GetRequiredService<IHytaleCredentialsRepository>();
        var authService = scope.ServiceProvider.GetRequiredService<IHytaleAuthService>();
        var notifier = scope.ServiceProvider.GetService<IHytaleTokenNotifier>();

        var expiringCredentials = await credentialsRepo.GetExpiringWithinAsync(_refreshWindow, ct).ConfigureAwait(false);

        if (expiringCredentials.Count > 0)
        {
            _logger.LogInformation(
                "Found {Count} Hytale tokens expiring within {Window}",
                expiringCredentials.Count,
                _refreshWindow);
        }

        foreach (var credentials in expiringCredentials)
        {
            _logger.LogInformation(
                "Refreshing Hytale token for user {UserId}, expires at {ExpiresAt}",
                credentials.UserId,
                credentials.ExpiresAt);

            var success = await authService.RefreshCredentialsAsync(credentials.UserId, ct).ConfigureAwait(false);

            if (success)
            {
                _logger.LogInformation(
                    "Successfully refreshed token for user {UserId}",
                    credentials.UserId);

                // Notify connected clients via SignalR
                if (notifier != null)
                {
                    // Re-fetch to get updated expiration time
                    var updated = await credentialsRepo.GetByUserIdAsync(credentials.UserId, ct).ConfigureAwait(false);
                    if (updated != null)
                    {
                        await notifier.NotifyTokenRefreshedAsync(credentials.UserId, updated.ExpiresAt, ct).ConfigureAwait(false);
                    }
                }
            }
            else
            {
                _logger.LogWarning(
                    "Failed to refresh token for user {UserId}. User may need to re-authenticate.",
                    credentials.UserId);

                // Notify user that re-authentication is needed
                if (notifier != null)
                {
                    await notifier.NotifyTokenRefreshFailedAsync(
                        credentials.UserId,
                        "Token refresh failed. Please re-authenticate with your Hytale account.",
                        ct).ConfigureAwait(false);
                }
            }
        }
    }
}
