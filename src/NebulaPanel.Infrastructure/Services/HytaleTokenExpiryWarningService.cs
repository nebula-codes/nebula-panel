namespace NebulaPanel.Infrastructure.Services;

using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Repositories;

/// <summary>
/// Background service that monitors token expiry and sends warnings to users.
/// </summary>
public class HytaleTokenExpiryWarningService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<HytaleTokenExpiryWarningService> _logger;
    private readonly HytaleAuthSettings _settings;

    /// <summary>
    /// How often to check for expiring tokens.
    /// </summary>
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Default warning intervals if not configured.
    /// </summary>
    private static readonly TimeSpan[] DefaultWarningIntervals =
    [
        TimeSpan.FromHours(24),
        TimeSpan.FromHours(6),
        TimeSpan.FromHours(1),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromMinutes(10)
    ];

    /// <summary>
    /// Tracks which warnings have been sent to avoid duplicates.
    /// Key: "userId:intervalTicks", Value: timestamp when warning was sent.
    /// </summary>
    private readonly ConcurrentDictionary<string, DateTime> _sentWarnings = new();

    public HytaleTokenExpiryWarningService(
        IServiceProvider services,
        IOptions<HytaleAuthSettings> settings,
        ILogger<HytaleTokenExpiryWarningService> logger)
    {
        _services = services;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Hytale token expiry warning service started. Check interval: {CheckInterval}",
            _checkInterval);

        // Wait a bit before first check to let app fully start
        await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndSendWarningsAsync(stoppingToken).ConfigureAwait(false);
                CleanupOldWarningRecords();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Hytale token expiry warning service");
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

        _logger.LogInformation("Hytale token expiry warning service stopped");
    }

    private async Task CheckAndSendWarningsAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var credentialsRepo = scope.ServiceProvider.GetRequiredService<IHytaleCredentialsRepository>();
        var authService = scope.ServiceProvider.GetRequiredService<IHytaleAuthService>();
        var notifier = scope.ServiceProvider.GetService<IHytaleTokenNotifier>();

        // Get all credentials
        var allCredentials = await credentialsRepo.GetAllAsync(ct).ConfigureAwait(false);

        // Get configured warning intervals, sorted descending (largest first)
        var warningIntervals = GetWarningIntervals()
            .OrderByDescending(x => x)
            .ToArray();

        foreach (var creds in allCredentials)
        {
            var remaining = creds.TimeRemaining;

            if (remaining <= TimeSpan.Zero)
            {
                // Token expired
                if (!HasSentWarning(creds.UserId, TimeSpan.Zero))
                {
                    _logger.LogWarning(
                        "Hytale token expired for user {UserId}",
                        creds.UserId);

                    // Notify via SignalR
                    if (notifier != null)
                    {
                        await notifier.NotifyTokenExpiredAsync(creds.UserId, ct).ConfigureAwait(false);
                    }

                    // Fire the expiration event
                    await authService.FireExpirationWarningAsync(creds.UserId, TimeSpan.Zero, ct).ConfigureAwait(false);

                    MarkWarningSent(creds.UserId, TimeSpan.Zero);
                }
            }
            else
            {
                // Check each warning interval
                foreach (var interval in warningIntervals)
                {
                    if (remaining <= interval)
                    {
                        // Within this warning window
                        if (!HasSentWarning(creds.UserId, interval))
                        {
                            _logger.LogInformation(
                                "Hytale token for user {UserId} expires in {Remaining} (warning at {Interval})",
                                creds.UserId,
                                remaining,
                                interval);

                            // Notify via SignalR
                            if (notifier != null)
                            {
                                await notifier.NotifyTokenExpiringWarningAsync(creds.UserId, remaining, ct).ConfigureAwait(false);
                            }

                            // Fire the expiration event
                            await authService.FireExpirationWarningAsync(creds.UserId, interval, ct).ConfigureAwait(false);

                            MarkWarningSent(creds.UserId, interval);
                        }

                        // Only send the most appropriate warning (don't send multiple)
                        break;
                    }
                }
            }
        }
    }

    private TimeSpan[] GetWarningIntervals()
    {
        if (_settings.ExpirationWarningThresholds.Count > 0)
        {
            return _settings.ExpirationWarningThresholds.ToArray();
        }

        return DefaultWarningIntervals;
    }

    private string GetWarningKey(Guid userId, TimeSpan interval)
    {
        return $"{userId}:{interval.Ticks}";
    }

    private bool HasSentWarning(Guid userId, TimeSpan interval)
    {
        var key = GetWarningKey(userId, interval);
        return _sentWarnings.ContainsKey(key);
    }

    private void MarkWarningSent(Guid userId, TimeSpan interval)
    {
        var key = GetWarningKey(userId, interval);
        _sentWarnings[key] = DateTime.UtcNow;
    }

    /// <summary>
    /// Removes old warning records (older than 48 hours) to prevent memory growth.
    /// </summary>
    private void CleanupOldWarningRecords()
    {
        var cutoff = DateTime.UtcNow.AddHours(-48);
        var keysToRemove = _sentWarnings
            .Where(kvp => kvp.Value < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _sentWarnings.TryRemove(key, out _);
        }

        if (keysToRemove.Count > 0)
        {
            _logger.LogDebug("Cleaned up {Count} old warning records", keysToRemove.Count);
        }
    }

    /// <summary>
    /// Clears sent warning records for a user (e.g., after token refresh).
    /// This allows warnings to be sent again for the new token lifecycle.
    /// </summary>
    public void ClearWarningsForUser(Guid userId)
    {
        var prefix = $"{userId}:";
        var keysToRemove = _sentWarnings.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.Ordinal))
            .ToList();

        foreach (var key in keysToRemove)
        {
            _sentWarnings.TryRemove(key, out _);
        }
    }

}
