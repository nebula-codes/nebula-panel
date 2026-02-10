using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.Interfaces.Configuration;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Infrastructure.Auth;
using NebulaPanel.Infrastructure.Configuration.Parsers;
using NebulaPanel.Infrastructure.FileManagement;
using NebulaPanel.Infrastructure.Executors;
using NebulaPanel.Infrastructure.Monitoring;
using NebulaPanel.Infrastructure.OfficialGames;
using NebulaPanel.Infrastructure.OfficialGames.Minecraft;
using NebulaPanel.Infrastructure.OfficialGames.Minecraft.Fetchers;
using NebulaPanel.Infrastructure.OfficialGames.Minecraft.Installers;
using NebulaPanel.Infrastructure.Repositories;
using NebulaPanel.Infrastructure.Rcon;
using NebulaPanel.Infrastructure.Services;
using NebulaPanel.Infrastructure.Configuration;
using NebulaPanel.Infrastructure.ModProviders;
using NebulaPanel.Infrastructure.ModProviders.CurseForge;
using NebulaPanel.Infrastructure.ModProviders.Modrinth;
using NebulaPanel.Infrastructure.ModProviders.Modtale;
using NebulaPanel.Infrastructure.ModpackProviders;
using NebulaPanel.Infrastructure.ModpackProviders.CurseForge;
using NebulaPanel.Infrastructure.ModpackProviders.FTB;
using NebulaPanel.Infrastructure.ModpackProviders.Modrinth;
using NebulaPanel.Infrastructure.Steam;
using NebulaPanel.Infrastructure.OfficialGames.Hytale;
using NebulaPanel.Infrastructure.OfficialGames.Terraria;
using NebulaPanel.Infrastructure.Security;
using NebulaPanel.Infrastructure.Health;
using NebulaPanel.Infrastructure.Webhooks;
using NebulaPanel.Infrastructure.Templates;

namespace NebulaPanel.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Repositories
        services.AddScoped<IGameRepository, GameRepository>();
        services.AddScoped<IGameServerRepository, GameServerRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IServerPermissionRepository, ServerPermissionRepository>();
        services.AddScoped<IServerModRepository, ServerModRepository>();
        services.AddScoped<IScheduledTaskRepository, ScheduledTaskRepository>();
        services.AddScoped<IBackupRepository, BackupRepository>();
        services.AddScoped<IUserActivityRepository, UserActivityRepository>();
        services.AddScoped<IServerActivityRepository, ServerActivityRepository>();
        services.AddScoped<IAnnouncementRepository, AnnouncementRepository>();
        services.AddScoped<IResourceUsageHistoryRepository, ResourceUsageHistoryRepository>();
        services.AddScoped<ISystemSettingsRepository, SystemSettingsRepository>();
        services.AddScoped<ICachedModRepository, CachedModRepository>();
        services.AddScoped<IModCacheSyncStatusRepository, ModCacheSyncStatusRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IAlertRuleRepository, AlertRuleRepository>();
        services.AddScoped<IWebhookEndpointRepository, WebhookEndpointRepository>();
        services.AddScoped<IWebhookDeliveryRepository, WebhookDeliveryRepository>();
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
        services.AddScoped<INodeRepository, NodeRepository>();

        // Database info service
        services.AddScoped<IDatabaseInfoService, DatabaseInfoService>();

        // Auth services
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IRoleService, RoleService>();

        // Rate limiting and security audit services
        services.AddSingleton<ILoginRateLimiter, LoginRateLimiter>();
        services.AddScoped<ISecurityAuditService, SecurityAuditService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IAuditQueryService, AuditQueryService>();

        // Leader election for background services (single-node always wins)
        services.AddSingleton<ILeaderElection, DatabaseLeaderElection>();

        // Server path resolver (auto-detects Docker mount mappings)
        services.AddSingleton<IServerPathResolver, ServerPathResolver>();

        // Executor state stores (in-memory for single-node, replaceable for multi-node)
        services.AddSingleton<NativeProcessStateStore>();
        services.AddSingleton<DockerContainerStateStore>();

        // Server executors
        services.AddSingleton<IServerExecutor, NativeProcessExecutor>();
        services.AddSingleton<IServerExecutor, DockerServerExecutor>();
        services.AddSingleton<IServerExecutorFactory, ServerExecutorFactory>();

        // Node-aware executor (routes local vs remote based on GameServer.NodeId)
        services.AddSingleton<AgentChannelManager>();
        services.AddSingleton<NodeAwareExecutorFactory>();
        services.AddSingleton<INodeAwareExecutorFactory>(sp => sp.GetRequiredService<NodeAwareExecutorFactory>());
        services.AddHostedService<NodeHeartbeatService>();

        // Steam integration
        services.AddHttpClient("SteamCMD");
        services.AddHttpClient("SteamApi");
        services.AddSingleton<ISteamCmdService, SteamCmdService>();
        services.AddSingleton<ISteamApiService, SteamApiService>();

        // Hytale integration
        services.AddHttpClient("HytaleDownloader");
        services.AddHttpClient("HytaleOAuth", client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("NebulaPanel/1.0 (game-server-panel)");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddSingleton<IHytaleDownloaderService, HytaleDownloaderService>();
        services.AddScoped<IHytaleOAuthClient, HytaleOAuthClient>();
        services.AddScoped<IHytaleCredentialsRepository, HytaleCredentialsRepository>();
        services.AddScoped<IHytalePreferencesRepository, HytalePreferencesRepository>();
        services.AddScoped<IHytaleAuthService, HytaleAuthService>();
        services.AddScoped<IHytaleSessionManager, HytaleSessionManager>();
        services.AddScoped<IHytalePreferencesService, HytalePreferencesService>();
        services.AddScoped<IHytaleServerInstallService, HytaleServerInstallService>();
        services.AddScoped<IHytaleUpdateService, HytaleUpdateService>();
        services.AddScoped<IHytaleWorldService, HytaleWorldService>();
        services.AddScoped<IHytaleModService, HytaleModService>();

        // Hytale token refresh and expiry monitoring
        services.AddHostedService<HytaleTokenRefreshService>();
        services.AddSingleton<HytaleTokenExpiryWarningService>();
        services.AddHostedService(sp => sp.GetRequiredService<HytaleTokenExpiryWarningService>());

        // Terraria/tModLoader integration
        services.AddHttpClient("Terraria", client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("NebulaPanel/1.0 (game-server-panel)");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddSingleton<TerrariaVersionFetcher>();
        services.AddSingleton<TerrariaServerInstaller>();
        services.AddScoped<ITerrariaModService, TerrariaModService>();

        // Encryption service (uses ASP.NET Core Data Protection)
        services.AddSingleton<IEncryptionService, DataProtectionEncryptionService>();

        // Integration settings provider (API keys from DB)
        services.AddSingleton<IIntegrationSettingsProvider, IntegrationSettingsProvider>();
        services.AddScoped<IApiKeyValidator, ApiKeyValidator>();
        services.AddHostedService<IntegrationSettingsMigrationService>();

        // Monitoring
        services.AddSingleton<ProcessResourceMonitor>();
        services.AddSingleton<IHostMonitorService, HostResourceMonitor>();
        services.AddHostedService<ResourceUsageCollector>();
        services.AddHostedService<SystemHealthBroadcaster>();

        // Health checks
        services.AddSingleton<DockerHealthCheck>();
        services.AddSingleton<ExternalApiHealthCheck>();
        services.AddSingleton<DiskSpaceHealthCheck>();
        services.AddSingleton<MemoryHealthCheck>();

        // Image handling
        services.AddHttpClient("ImageDownload");
        services.AddScoped<IImageService, ImageService>();

        // File management
        services.AddScoped<IServerFileManager, ServerFileManager>();
        services.AddScoped<IBackupFileManager, BackupFileManager>();

        // RCON service
        services.AddSingleton<Domain.Interfaces.IRconService, RconService>();

        // Minecraft version fetching
        services.AddHttpClient("Minecraft", client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("NebulaPanel/1.0");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Minecraft version fetchers
        services.AddSingleton<IMinecraftVersionFetcher, VanillaVersionFetcher>();
        services.AddSingleton<IMinecraftVersionFetcher, PaperVersionFetcher>();
        services.AddSingleton<IMinecraftVersionFetcher, PurpurVersionFetcher>();
        services.AddSingleton<IMinecraftVersionFetcher, FabricVersionFetcher>();
        services.AddSingleton<IMinecraftVersionFetcher, ForgeVersionFetcher>();
        services.AddSingleton<IMinecraftVersionFetcher, SpigotVersionFetcher>();

        // Minecraft version aggregator
        services.AddSingleton<MinecraftVersionAggregator>();

        // Minecraft Java runtime manager (auto-downloads Adoptium JREs)
        services.AddHttpClient("Adoptium", client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("NebulaPanel/1.0 (game-server-panel)");
            client.Timeout = TimeSpan.FromMinutes(10); // JRE downloads can be large
        });
        services.AddSingleton<JavaRuntimeManager>();

        // Minecraft Java detector
        services.AddSingleton<MinecraftJavaDetector>();

        // Minecraft loader installers
        services.AddSingleton<IMinecraftLoaderInstaller, VanillaInstaller>();
        services.AddSingleton<IMinecraftLoaderInstaller, PaperInstaller>();
        services.AddSingleton<IMinecraftLoaderInstaller, PurpurInstaller>();
        services.AddSingleton<IMinecraftLoaderInstaller, FabricInstaller>();
        services.AddSingleton<IMinecraftLoaderInstaller, ForgeInstaller>();
        services.AddSingleton<IMinecraftLoaderInstaller, NeoForgeInstaller>();
        services.AddSingleton<IMinecraftLoaderInstaller, QuiltInstaller>();
        services.AddSingleton<IMinecraftLoaderInstaller, SpigotInstaller>();

        // Minecraft server installer coordinator
        services.AddSingleton<MinecraftServerInstaller>();

        // Minecraft config writer
        services.AddSingleton<IMinecraftConfigWriter, MinecraftConfigWriter>();

        // Official game providers and registry
        services.AddSingleton<IOfficialGameProvider, MinecraftProvider>();
        services.AddSingleton<IOfficialGameProvider, HytaleGameProvider>();
        services.AddSingleton<IOfficialGameProvider, TerrariaProvider>();
        // services.AddSingleton<IOfficialGameProvider, ValheimProvider>();
        services.AddSingleton<IOfficialGameRegistry, OfficialGameRegistry>();

        // Configuration schema loading
        services.AddSingleton<IConfigurationSchemaLoader, ConfigurationSchemaLoader>();

        // Configuration file parsers
        services.AddSingleton<IConfigFileParser, PropertiesFileParser>();
        services.AddSingleton<IConfigFileParser, JsonFileParser>();
        services.AddSingleton<IConfigFileParser, IniFileParser>();
        services.AddSingleton<IConfigFileParser, YamlFileParser>();
        services.AddSingleton<IConfigParserFactory, ConfigParserFactory>();

        // Official game discovery service (runs on startup)
        services.AddHostedService<OfficialGameDiscoveryService>();

        // HTTP client for JSON-based game providers
        services.AddHttpClient("JsonGameProvider", client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("NebulaPanel/1.0");
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        // Official game service for admin UI
        services.AddScoped<IOfficialGameService, OfficialGameService>();

        // Mod providers
        services.AddHttpClient("Modrinth", client =>
        {
            client.BaseAddress = new Uri("https://api.modrinth.com/v2/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("NebulaPanel/1.0 (game-server-panel)");
            client.Timeout = TimeSpan.FromSeconds(120); // Longer timeout for bulk sync operations
        });
        services.AddSingleton<ModrinthApiClient>();
        services.AddSingleton<IModProvider, ModrinthProvider>();

        // CurseForge mod provider
        services.AddHttpClient("CurseForge", client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("NebulaPanel/1.0 (game-server-panel)");
            client.Timeout = TimeSpan.FromSeconds(120); // Longer timeout for bulk sync operations
        });
        services.AddSingleton<CurseForgeApiClient>();
        services.AddSingleton<IModProvider, CurseForgeProvider>();

        // Modtale mod provider (Hytale)
        services.AddHttpClient("Modtale", client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("NebulaPanel/1.0 (game-server-panel)");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddSingleton<ModtaleApiClient>();
        services.AddSingleton<IModProvider, ModtaleProvider>();

        // Steam Workshop mod provider
        services.AddSingleton<IModProvider, SteamWorkshopProvider>();

        // FTB modpack provider (no auth required)
        services.AddHttpClient("FTB", client =>
        {
            client.BaseAddress = new Uri("https://api.modpacks.ch/public/");
            client.DefaultRequestHeaders.UserAgent.ParseAdd("NebulaPanel/1.0 (game-server-panel)");
            client.Timeout = TimeSpan.FromSeconds(120);
        });
        services.AddSingleton<FtbApiClient>();

        // Modpack providers
        services.AddSingleton<IModpackProvider, CurseForgeModpackProvider>();
        services.AddSingleton<IModpackProvider, ModrinthModpackProvider>();
        services.AddSingleton<IModpackProvider, FtbModpackProvider>();
        services.AddSingleton<IModpackProviderRegistry, ModpackProviderRegistry>();

        // Modpack installation services
        services.AddSingleton<ModpackLoaderInstaller>();
        services.AddScoped<ModpackServerConfigurator>();
        services.AddScoped<IModpackInstallationService, ModpackInstallationService>();

        // Unified modpack service (aggregates all providers)
        services.AddScoped<IUnifiedModpackService, UnifiedModpackService>();

        // Modpack update services
        services.AddScoped<ModpackFilePreserver>();
        services.AddScoped<IModpackUpdateService, ModpackUpdateService>();

        // Scheduled task job manager
        services.AddSingleton<IScheduledTaskJobManager, HangfireJobManager>();

        // Mod cache sync service
        services.AddScoped<IModCacheSyncService, ModCacheSyncService>();

        // Mod cache job manager (runs on startup to register Hangfire jobs)
        services.AddSingleton<ModCacheJobManager>();
        services.AddHostedService(sp => sp.GetRequiredService<ModCacheJobManager>());

        // Update service
        services.AddScoped<IUpdateService, UpdateService>();
        services.AddScoped<IUpdateHistoryService, UpdateHistoryService>();
        services.AddScoped<IUpdateScheduleService, UpdateScheduleService>();
        services.AddHostedService<UpdateBackgroundService>();
        services.AddHostedService<UpdateScheduleBackgroundService>();
        services.AddScoped<PostUpdateService>();

        // Log viewer service
        services.AddScoped<ILogViewerService, LogViewerService>();

        // Webhooks
        services.AddHttpClient("Webhooks", client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("NebulaPanel/1.0 (webhook)");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IWebhookDispatcher, HttpWebhookDispatcher>();

        // Community game templates
        services.AddHttpClient("CommunityTemplates", client =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("NebulaPanel/1.0");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddSingleton<ICommunityTemplateRepository, GitHubCommunityTemplateRepository>();

        // RCON password migration (one-time startup)
        services.AddHostedService<RconPasswordMigrationService>();

        return services;
    }
}
