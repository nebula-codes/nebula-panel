using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.Settings;
using NebulaPanel.Infrastructure.Configuration;
using NebulaPanel.Infrastructure.Persistence;

namespace NebulaPanel.Infrastructure.Services;

public class IntegrationSettingsMigrationService(
    IServiceScopeFactory scopeFactory,
    IEncryptionService encryptionService,
    IOptions<CurseForgeSettings> curseForgeSettings,
    IOptions<SteamApiSettings> steamSettings,
    IOptions<ModtaleSettings> modtaleSettings,
    ILogger<IntegrationSettingsMigrationService> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small delay to let the DB be ready
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);

        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<NebulaPanelDbContext>();

            var settings = await dbContext.Set<SystemSettings>()
                .FirstOrDefaultAsync(s => s.Id == SystemSettings.SingletonId, stoppingToken)
                .ConfigureAwait(false);

            if (settings is null)
                return;

            // Only migrate if integration settings are empty
            if (!string.IsNullOrWhiteSpace(settings.IntegrationSettingsJson) &&
                settings.IntegrationSettingsJson != "{}")
            {
                return;
            }

            var curseForgeKey = curseForgeSettings.Value.ApiKey;
            var steamKey = steamSettings.Value.ApiKey;
            var modtaleKey = modtaleSettings.Value.ApiKey;

            var hasAnyKey = !string.IsNullOrWhiteSpace(curseForgeKey)
                         || !string.IsNullOrWhiteSpace(steamKey)
                         || !string.IsNullOrWhiteSpace(modtaleKey);

            if (!hasAnyKey)
                return;

            var data = new IntegrationSettingsData
            {
                CurseForgeApiKey = !string.IsNullOrWhiteSpace(curseForgeKey)
                    ? encryptionService.Encrypt(curseForgeKey)
                    : null,
                SteamApiKey = !string.IsNullOrWhiteSpace(steamKey)
                    ? encryptionService.Encrypt(steamKey)
                    : null,
                ModtaleApiKey = !string.IsNullOrWhiteSpace(modtaleKey)
                    ? encryptionService.Encrypt(modtaleKey)
                    : null
            };

            settings.IntegrationSettingsJson = JsonSerializer.Serialize(data, JsonOptions);
            await dbContext.SaveChangesAsync(stoppingToken).ConfigureAwait(false);

            var migrated = new List<string>();
            if (!string.IsNullOrWhiteSpace(curseForgeKey)) migrated.Add("CurseForge");
            if (!string.IsNullOrWhiteSpace(steamKey)) migrated.Add("Steam");
            if (!string.IsNullOrWhiteSpace(modtaleKey)) migrated.Add("Modtale");

            logger.LogInformation(
                "Migrated API keys from appsettings.json to database: {Providers}",
                string.Join(", ", migrated));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to migrate integration settings from appsettings.json (non-fatal)");
        }
    }

    private class IntegrationSettingsData
    {
        public string? CurseForgeApiKey { get; set; }
        public string? SteamApiKey { get; set; }
        public string? ModtaleApiKey { get; set; }
    }
}
