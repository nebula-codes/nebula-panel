using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Infrastructure.Persistence;

namespace NebulaPanel.Infrastructure.Services;

/// <summary>
/// One-time startup service that encrypts any remaining plain text RCON passwords.
/// Stores a migration flag in SystemSettings.GeneralSettingsJson to avoid re-running.
/// </summary>
public class RconPasswordMigrationService(
    IServiceScopeFactory scopeFactory,
    ILogger<RconPasswordMigrationService> logger) : IHostedService
{
    private const string MigrationFlag = "rconPasswordsEncrypted";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NebulaPanelDbContext>();
        var encryption = scope.ServiceProvider.GetRequiredService<IEncryptionService>();

        // Check if migration has already run via a flag in GeneralSettings
        var settings = await context.SystemSettings
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (settings is not null)
        {
            try
            {
                var generalJson = JsonDocument.Parse(settings.GeneralSettingsJson);
                if (generalJson.RootElement.TryGetProperty(MigrationFlag, out var flag) && flag.GetBoolean())
                {
                    return;
                }
            }
            catch
            {
                // JSON parse error — continue with migration
            }
        }

        logger.LogInformation("Migrating plain text RCON passwords to encrypted format...");

        var servers = await context.GameServers
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var migrated = 0;
        foreach (var server in servers)
        {
            if (server.RconConfig?.Password is not null && !string.IsNullOrEmpty(server.RconConfig.Password))
            {
                // Try to decrypt — if it works, it's already encrypted
                try
                {
                    encryption.Decrypt(server.RconConfig.Password);
                    continue; // Already encrypted
                }
                catch
                {
                    // Not encrypted yet — encrypt it
                    server.RconConfig.Password = encryption.Encrypt(server.RconConfig.Password);
                    migrated++;
                }
            }
        }

        if (migrated > 0)
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("Encrypted RCON passwords for {Count} servers", migrated);
        }

        // Mark migration as complete
        if (settings is not null)
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(settings.GeneralSettingsJson)
                       ?? [];
            dict[MigrationFlag] = JsonSerializer.SerializeToElement(true);
            settings.GeneralSettingsJson = JsonSerializer.Serialize(dict);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
