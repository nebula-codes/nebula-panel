namespace NebulaPanel.Infrastructure.Auth;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

/// <summary>
/// In-memory sliding window rate limiter for login attempts.
/// </summary>
public class LoginRateLimiter(
    IMemoryCache cache,
    IOptions<RateLimitSettings> settings) : ILoginRateLimiter
{
    private readonly RateLimitSettings _settings = settings.Value;
    private const string CacheKeyPrefix = "RateLimit:Login:";

    public bool IsAllowed(string? ipAddress, string? username)
    {
        var key = GetCacheKey(ipAddress, username);
        var attempts = GetAttempts(key);

        return attempts.Count < _settings.MaxAttemptsPerWindow;
    }

    public void RecordAttempt(string? ipAddress, string? username)
    {
        var key = GetCacheKey(ipAddress, username);
        var attempts = GetAttempts(key);
        var now = DateTime.UtcNow;

        // Create a new list to avoid mutating a list that may be iterated concurrently
        var updated = new List<DateTime>(attempts) { now };

        // Store with expiration based on window size
        var cacheOptions = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(_settings.WindowSeconds * 2)
        };

        cache.Set(key, updated, cacheOptions);
    }

    public int GetRetryAfterSeconds(string? ipAddress, string? username)
    {
        var key = GetCacheKey(ipAddress, username);
        var attempts = GetAttempts(key);

        if (attempts.Count < _settings.MaxAttemptsPerWindow)
        {
            return 0;
        }

        // Find the oldest attempt that's still within the window
        // The rate limit will reset when that attempt expires
        var windowStart = DateTime.UtcNow.AddSeconds(-_settings.WindowSeconds);
        var oldestInWindow = attempts.Where(a => a > windowStart).OrderBy(a => a).FirstOrDefault();

        if (oldestInWindow == default)
        {
            return 0;
        }

        var resetTime = oldestInWindow.AddSeconds(_settings.WindowSeconds);
        var secondsUntilReset = (int)Math.Ceiling((resetTime - DateTime.UtcNow).TotalSeconds);

        return Math.Max(0, secondsUntilReset);
    }

    public void ClearAttempts(string? ipAddress, string? username)
    {
        var key = GetCacheKey(ipAddress, username);
        cache.Remove(key);
    }

    private string GetCacheKey(string? ipAddress, string? username)
    {
        var normalizedIp = string.IsNullOrEmpty(ipAddress) ? "unknown" : ipAddress;

        if (_settings.IncludeUsername && !string.IsNullOrEmpty(username))
        {
            return $"{CacheKeyPrefix}{normalizedIp}:{username.ToLowerInvariant()}";
        }

        return $"{CacheKeyPrefix}{normalizedIp}";
    }

    private List<DateTime> GetAttempts(string key)
    {
        if (!cache.TryGetValue<List<DateTime>>(key, out var attempts) || attempts is null)
        {
            return [];
        }

        // Filter out attempts outside the current window
        var windowStart = DateTime.UtcNow.AddSeconds(-_settings.WindowSeconds);
        return attempts.Where(a => a > windowStart).ToList();
    }
}
