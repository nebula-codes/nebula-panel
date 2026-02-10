using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.ValueObjects;
using NebulaPanel.Infrastructure.Persistence;

namespace NebulaPanel.Infrastructure.Services;

public sealed class IntegrationSettingsProvider : IIntegrationSettingsProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<IntegrationSettingsProvider> _logger;
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    private IntegrationSettings? _cached;
    private DateTime _cacheExpiry = DateTime.MinValue;

    public IntegrationSettingsProvider(
        IServiceScopeFactory scopeFactory,
        IEncryptionService encryptionService,
        ILogger<IntegrationSettingsProvider> logger)
    {
        _scopeFactory = scopeFactory;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public string? GetCurseForgeApiKey() => GetCachedSettings().CurseForgeApiKey;

    public string? GetSteamApiKey() => GetCachedSettings().SteamApiKey;

    public string? GetModtaleApiKey() => GetCachedSettings().ModtaleApiKey;

    public void InvalidateCache()
    {
        _cached = null;
        _cacheExpiry = DateTime.MinValue;
    }

    /// <summary>
    /// Returns cached settings, refreshing from database if the cache has expired.
    /// Note: This intentionally uses synchronous semaphore Wait() and synchronous EF Core
    /// (FirstOrDefault) because the callers are sync properties (GetCurseForgeApiKey, etc.).
    /// The 60-second cache ensures the blocking DB call is rare.
    /// </summary>
    private IntegrationSettings GetCachedSettings()
    {
        if (_cached != null && DateTime.UtcNow < _cacheExpiry)
            return _cached;

        _semaphore.Wait();
        try
        {
            // Double-check after acquiring lock
            if (_cached != null && DateTime.UtcNow < _cacheExpiry)
                return _cached;

            _cached = LoadFromDatabase();
            _cacheExpiry = DateTime.UtcNow.Add(CacheTtl);
            return _cached;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private IntegrationSettings LoadFromDatabase()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NebulaPanelDbContext>();

            var settings = dbContext.Set<SystemSettings>()
                .AsNoTracking()
                .FirstOrDefault(s => s.Id == SystemSettings.SingletonId);

            if (settings is null)
                return new IntegrationSettings();

            var json = settings.IntegrationSettingsJson;
            if (string.IsNullOrWhiteSpace(json) || json == "{}")
                return new IntegrationSettings();

            var data = JsonSerializer.Deserialize<IntegrationSettingsData>(json, JsonOptions);
            if (data is null)
                return new IntegrationSettings();

            return new IntegrationSettings
            {
                CurseForgeApiKey = TryDecrypt(data.CurseForgeApiKey),
                SteamApiKey = TryDecrypt(data.SteamApiKey),
                ModtaleApiKey = TryDecrypt(data.ModtaleApiKey)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load integration settings from database");
            return new IntegrationSettings();
        }
    }

    private string? TryDecrypt(string? encrypted)
    {
        if (string.IsNullOrWhiteSpace(encrypted))
            return null;

        try
        {
            return _encryptionService.Decrypt(encrypted);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to decrypt integration setting, treating as unconfigured");
            return null;
        }
    }

    private class IntegrationSettingsData
    {
        public string? CurseForgeApiKey { get; set; }
        public string? SteamApiKey { get; set; }
        public string? ModtaleApiKey { get; set; }
    }
}
