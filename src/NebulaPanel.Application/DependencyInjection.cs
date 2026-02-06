using Microsoft.Extensions.DependencyInjection;
using NebulaPanel.Application.Services;

namespace NebulaPanel.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Services
        services.AddScoped<IGameService, GameService>();
        services.AddScoped<IGameServerService, GameServerService>();
        services.AddScoped<IMinecraftInstallService, MinecraftInstallService>();
        services.AddScoped<IFileExplorerService, FileExplorerService>();
        services.AddScoped<IConfigurationService, ConfigurationService>();
        services.AddScoped<IUnifiedModService, UnifiedModService>();
        services.AddScoped<IScheduledTaskService, ScheduledTaskService>();
        services.AddScoped<IBackupService, BackupService>();
        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IModCacheService, ModCacheService>();
        services.AddScoped<INotificationService, NotificationService>();

        // Alert rules
        services.AddScoped<IAlertRuleService, AlertRuleService>();

        // Webhooks & API keys
        services.AddScoped<IWebhookService, WebhookService>();
        services.AddScoped<IApiKeyService, ApiKeyService>();

        // Dashboard services
        services.AddScoped<IServerActivityService, ServerActivityService>();
        services.AddScoped<IAnnouncementService, AnnouncementService>();
        services.AddScoped<IDashboardService, DashboardService>();

        // Steam game presets
        services.AddSingleton<ISteamGamePresetService, SteamGamePresetService>();

        return services;
    }
}
