using System.Text.Json;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.Services;

/// <summary>
/// Manages Hangfire jobs for mod cache synchronization.
/// Registers recurring jobs on startup and handles initial sync.
/// </summary>
public class ModCacheJobManager : IHostedService
{
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ModCacheJobManager> _logger;

    private const string IncrementalJobId = "mod-cache-incremental";
    private const string FullSyncModrinthModsJobId = "mod-cache-modrinth-mods-full";
    private const string FullSyncModrinthModpacksJobId = "mod-cache-modrinth-modpacks-full";
    private const string FullSyncCurseForgeModsJobId = "mod-cache-curseforge-mods-full";
    private const string FullSyncCurseForgeModpacksJobId = "mod-cache-curseforge-modpacks-full";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public ModCacheJobManager(
        IRecurringJobManager recurringJobManager,
        IBackgroundJobClient backgroundJobClient,
        IServiceProvider serviceProvider,
        ILogger<ModCacheJobManager> logger)
    {
        _recurringJobManager = recurringJobManager;
        _backgroundJobClient = backgroundJobClient;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ModCacheJobManager starting...");

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var settingsRepository = scope.ServiceProvider.GetRequiredService<ISystemSettingsRepository>();
            var syncStatusRepository = scope.ServiceProvider.GetRequiredService<IModCacheSyncStatusRepository>();

            // Use CancellationToken.None for startup DB operations - these must complete for proper initialization
            // The shutdown token should only be used for long-running operations that need to be interruptible
            var systemSettings = await settingsRepository.GetAsync(CancellationToken.None).ConfigureAwait(false);
            var settings = DeserializeSettings(systemSettings.ModCacheSettingsJson);

            if (!settings.Enabled)
            {
                _logger.LogInformation("Mod caching is disabled, skipping job registration");
                RemoveAllJobs();
                return;
            }

            // Register recurring jobs
            RegisterRecurringJobs(settings);

            // Check if initial sync is needed
            var syncStatuses = await syncStatusRepository.GetAllAsync(CancellationToken.None).ConfigureAwait(false);
            var hasCompletedSync = syncStatuses.Any(s => s.LastFullSyncCompletedAt.HasValue);

            if (!hasCompletedSync)
            {
                _logger.LogInformation("No completed sync found, queueing initial sync");
                QueueInitialSync(settings);
            }
            else
            {
                _logger.LogInformation("Mod cache already has completed sync, skipping initial sync");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize ModCacheJobManager");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("ModCacheJobManager stopping...");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Updates the recurring jobs when settings change.
    /// </summary>
    public void UpdateJobs(ModCacheSettings settings)
    {
        if (!settings.Enabled)
        {
            RemoveAllJobs();
            return;
        }

        RegisterRecurringJobs(settings);
    }

    private void RegisterRecurringJobs(ModCacheSettings settings)
    {
        // Incremental sync job (runs frequently)
        _recurringJobManager.AddOrUpdate<IModCacheSyncService>(
            IncrementalJobId,
            service => service.TriggerManualSyncAsync(false, CancellationToken.None),
            settings.IncrementalSyncCron,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        _logger.LogInformation("Registered incremental sync job with cron: {Cron}", settings.IncrementalSyncCron);

        // Full sync jobs (run less frequently)
        if (settings.ModrinthEnabled)
        {
            _recurringJobManager.AddOrUpdate<IModCacheSyncService>(
                FullSyncModrinthModsJobId,
                service => service.FullSyncAsync(ModProviderType.Modrinth, ModContentType.Mod, CancellationToken.None),
                settings.FullSyncCron,
                new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

            if (settings.CacheModpacks)
            {
                _recurringJobManager.AddOrUpdate<IModCacheSyncService>(
                    FullSyncModrinthModpacksJobId,
                    service => service.FullSyncAsync(ModProviderType.Modrinth, ModContentType.Modpack, CancellationToken.None),
                    settings.FullSyncCron,
                    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
            }
            else
            {
                _recurringJobManager.RemoveIfExists(FullSyncModrinthModpacksJobId);
            }
        }
        else
        {
            _recurringJobManager.RemoveIfExists(FullSyncModrinthModsJobId);
            _recurringJobManager.RemoveIfExists(FullSyncModrinthModpacksJobId);
        }

        if (settings.CurseForgeEnabled)
        {
            _recurringJobManager.AddOrUpdate<IModCacheSyncService>(
                FullSyncCurseForgeModsJobId,
                service => service.FullSyncAsync(ModProviderType.CurseForge, ModContentType.Mod, CancellationToken.None),
                settings.FullSyncCron,
                new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

            if (settings.CacheModpacks)
            {
                _recurringJobManager.AddOrUpdate<IModCacheSyncService>(
                    FullSyncCurseForgeModpacksJobId,
                    service => service.FullSyncAsync(ModProviderType.CurseForge, ModContentType.Modpack, CancellationToken.None),
                    settings.FullSyncCron,
                    new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
            }
            else
            {
                _recurringJobManager.RemoveIfExists(FullSyncCurseForgeModpacksJobId);
            }
        }
        else
        {
            _recurringJobManager.RemoveIfExists(FullSyncCurseForgeModsJobId);
            _recurringJobManager.RemoveIfExists(FullSyncCurseForgeModpacksJobId);
        }

        _logger.LogInformation("Registered full sync jobs with cron: {Cron}", settings.FullSyncCron);
    }

    private void RemoveAllJobs()
    {
        _recurringJobManager.RemoveIfExists(IncrementalJobId);
        _recurringJobManager.RemoveIfExists(FullSyncModrinthModsJobId);
        _recurringJobManager.RemoveIfExists(FullSyncModrinthModpacksJobId);
        _recurringJobManager.RemoveIfExists(FullSyncCurseForgeModsJobId);
        _recurringJobManager.RemoveIfExists(FullSyncCurseForgeModpacksJobId);
        _logger.LogInformation("Removed all mod cache jobs");
    }

    private void QueueInitialSync(ModCacheSettings settings)
    {
        // Queue initial syncs in order: Modrinth first (smaller), then CurseForge
        // This runs as a background job so it doesn't block app startup

        if (settings.ModrinthEnabled)
        {
            _backgroundJobClient.Enqueue<IModCacheSyncService>(
                service => service.FullSyncAsync(ModProviderType.Modrinth, ModContentType.Mod, CancellationToken.None));

            if (settings.CacheModpacks)
            {
                _backgroundJobClient.Enqueue<IModCacheSyncService>(
                    service => service.FullSyncAsync(ModProviderType.Modrinth, ModContentType.Modpack, CancellationToken.None));
            }
        }

        if (settings.CurseForgeEnabled)
        {
            _backgroundJobClient.Enqueue<IModCacheSyncService>(
                service => service.FullSyncAsync(ModProviderType.CurseForge, ModContentType.Mod, CancellationToken.None));

            if (settings.CacheModpacks)
            {
                _backgroundJobClient.Enqueue<IModCacheSyncService>(
                    service => service.FullSyncAsync(ModProviderType.CurseForge, ModContentType.Modpack, CancellationToken.None));
            }
        }

        _logger.LogInformation("Queued initial sync jobs");
    }

    private static ModCacheSettings DeserializeSettings(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
        {
            return new ModCacheSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<ModCacheSettings>(json, JsonOptions) ?? new ModCacheSettings();
        }
        catch
        {
            return new ModCacheSettings();
        }
    }
}
