using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.Persistence;

/// <summary>
/// Seeds official games from registered providers into the database.
/// Preserves user-configurable settings while updating provider definitions.
/// </summary>
public static class OfficialGameSeeder
{
    private const string DefaultSchemaVersion = "1.0";

    public static async Task SeedOfficialGamesAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NebulaPanelDbContext>();
        var registry = scope.ServiceProvider.GetRequiredService<IOfficialGameRegistry>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<NebulaPanelDbContext>>();

        logger.LogInformation("Seeding official games from {Count} providers...", registry.GetAllProviders().Count());

        foreach (var provider in registry.GetAllProviders())
        {
            var definition = provider.GetGameDefinition();
            await SeedOrUpdateGameAsync(context, definition, registry, logger).ConfigureAwait(false);
        }

        logger.LogInformation("Official game seeding complete");
    }

    private static async Task SeedOrUpdateGameAsync(
        NebulaPanelDbContext context,
        GameDefinition definition,
        IOfficialGameRegistry registry,
        ILogger logger)
    {
        var slug = definition.Slug.ToLowerInvariant();
        var existing = await context.Games
            .FirstOrDefaultAsync(g => g.Slug == slug)
            .ConfigureAwait(false);

        if (existing is null)
        {
            // Create new official game
            var game = new Game
            {
                Id = Guid.NewGuid(),
                Name = definition.Name,
                Slug = slug,
                SourceType = GameSourceType.Official,
                SteamAppId = definition.SteamAppId,
                ExecutableType = definition.ExecutableType,
                DefaultExecutablePath = definition.DefaultExecutablePath,
                DefaultStartCommand = definition.DefaultStartCommand,
                DefaultStopCommand = definition.DefaultStopCommand,
                SupportsDocker = definition.SupportsDocker,
                DefaultDockerImage = definition.DefaultDockerImage,
                DockerDataPath = definition.DockerDataPath,
                DefaultPort = definition.DefaultPort,
                IconPath = definition.IconPath,
                SupportsMods = definition.SupportsMods,
                ModProviders = definition.ModProviders,
                RconDefaults = definition.RconDefaults,
                ConfigurationSchemas = definition.ConfigurationSchemas,
                // New properties for official game management
                IsEnabled = true,
                SchemaVersion = DefaultSchemaVersion
            };

            context.Games.Add(game);
            await context.SaveChangesAsync().ConfigureAwait(false);
            logger.LogInformation("Seeded official game: {GameName} ({Slug})", definition.Name, slug);
        }
        else if (existing.SourceType == GameSourceType.Official)
        {
            // Update existing official game definition (refresh configs on app update)
            // Preserve user-configurable properties: IsEnabled, IconPath, LastVersionCheck, CachedVersionCount
            existing.Name = definition.Name;
            existing.SteamAppId = definition.SteamAppId;
            existing.ExecutableType = definition.ExecutableType;
            existing.DefaultExecutablePath = definition.DefaultExecutablePath;
            existing.DefaultStartCommand = definition.DefaultStartCommand;
            existing.DefaultStopCommand = definition.DefaultStopCommand;
            existing.SupportsDocker = definition.SupportsDocker;
            existing.DefaultDockerImage = definition.DefaultDockerImage;
            existing.DockerDataPath = definition.DockerDataPath;
            existing.DefaultPort = definition.DefaultPort;
            existing.SupportsMods = definition.SupportsMods;
            existing.ModProviders = definition.ModProviders;
            existing.RconDefaults = definition.RconDefaults;
            existing.ConfigurationSchemas = definition.ConfigurationSchemas;
            // Update schema version if schemas changed
            existing.SchemaVersion = DefaultSchemaVersion;
            // Set IconPath if not already set (preserve user customizations, but fix missing icons)
            if (string.IsNullOrEmpty(existing.IconPath) && !string.IsNullOrEmpty(definition.IconPath))
            {
                existing.IconPath = definition.IconPath;
            }
            // Note: IsEnabled, LastVersionCheck, CachedVersionCount are preserved

            await context.SaveChangesAsync().ConfigureAwait(false);
            logger.LogDebug("Updated official game definition: {GameName}", definition.Name);

            // Sync metadata with registry
            if (existing.CachedVersionCount.HasValue && existing.LastVersionCheck.HasValue)
            {
                registry.UpdateProviderMetadata(slug, existing.CachedVersionCount.Value, existing.LastVersionCheck.Value);
            }
        }
        // If existing but Custom, don't overwrite - user created a game with the same slug
        else
        {
            logger.LogWarning(
                "Skipping official game '{GameName}' - a custom game with slug '{Slug}' already exists",
                definition.Name,
                slug);
        }
    }
}
