using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.OfficialGames.Hytale;

public class HytaleGameProvider(
    IHytaleDownloaderService downloader,
    IServiceScopeFactory scopeFactory,
    IServerPathResolver pathResolver,
    ILogger<HytaleGameProvider> logger) : IOfficialGameProvider
{
    public string GameSlug => "hytale";

    public GameDefinition GetGameDefinition() => new()
    {
        Name = "Hytale",
        Slug = "hytale",
        ExecutableType = ExecutableType.Exe,
        DefaultExecutablePath = "hytale-server",
        DefaultStartCommand = "./hytale-server",
        SupportsDocker = true,
        DefaultDockerImage = null, // TBD - may need custom image
        IconPath = "/images/games/hytale.png",
        SupportsMods = true,
        ModProviders =
        [
            new ModProviderConfiguration
            {
                Provider = ModProviderType.Modtale,
                Enabled = true,
                Priority = 1,
                ModInstallPath = "mods/"
            },
            new ModProviderConfiguration
            {
                Provider = ModProviderType.Local,
                Enabled = true,
                Priority = 2,
                ModInstallPath = "mods/"
            }
        ],
        RconDefaults = null, // TBD - check if Hytale has RCON
        ConfigurationSchemas = GetConfigurationSchemas()
    };

    public string GetDefaultInstallPath(string serverName)
    {
        var sanitized = SanitizeForPath(serverName);
        var basePath = pathResolver.GetServerBasePath();

        return OperatingSystem.IsWindows()
            ? Path.Combine(basePath, "HytaleServers", sanitized)
            : Path.Combine(basePath, "hytale-servers", sanitized);
    }

    private static string SanitizeForPath(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "server";

        var sanitized = name.ToLowerInvariant()
            .Replace(' ', '-')
            .Replace('_', '-');

        sanitized = new string(sanitized.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());

        while (sanitized.Contains("--"))
            sanitized = sanitized.Replace("--", "-");

        sanitized = sanitized.Trim('-');
        return string.IsNullOrEmpty(sanitized) ? "server" : sanitized;
    }

    public async Task<IReadOnlyList<GameVersionInfo>> GetAvailableVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        var versions = new List<GameVersionInfo>();

        // Get release version
        try
        {
            var release = await downloader.GetServerVersionAsync("release", null, cancellationToken)
                .ConfigureAwait(false);
            if (release != null)
            {
                versions.Add(new GameVersionInfo
                {
                    Version = $"{release.Version}:release",
                    DisplayName = $"{release.Version} (Release)",
                    VersionType = GameVersionType.Release,
                    IsRecommended = true
                });
            }
            else
            {
                logger.LogWarning("Failed to fetch Hytale release version - API returned null");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch Hytale release version");
        }

        // Get pre-release version
        try
        {
            var prerelease = await downloader.GetServerVersionAsync("pre-release", null, cancellationToken)
                .ConfigureAwait(false);
            if (prerelease != null)
            {
                versions.Add(new GameVersionInfo
                {
                    Version = $"{prerelease.Version}:pre-release",
                    DisplayName = $"{prerelease.Version} (Pre-release)",
                    VersionType = GameVersionType.Beta
                });
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Pre-release version not available");
        }

        return versions;
    }

    public async Task<ServerInstallationResult> InstallServerAsync(
        GameServer server,
        string version,
        IProgress<OfficialInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Parse version string (format: "version:patchline")
        var (_, patchline) = ParseVersionString(version);

        // Get user's Hytale credentials using a scope for the scoped auth service
        await using var scope = scopeFactory.CreateAsyncScope();
        var authService = scope.ServiceProvider.GetRequiredService<IHytaleAuthService>();
        var credentials = await authService.GetDecryptedCredentialsAsync(server.OwnerId, cancellationToken)
            .ConfigureAwait(false);

        if (credentials == null || !credentials.IsValid)
        {
            return ServerInstallationResult.Failed(
                "Hytale authentication required. Please connect your Hytale account.");
        }

        Directory.CreateDirectory(server.InstallPath);

        progress?.Report(OfficialInstallProgress.Starting("Downloading"));

        var downloadResult = await downloader.DownloadServerAsync(
            server.InstallPath,
            patchline,
            credentials,
            new Progress<HytaleDownloadProgress>(p =>
            {
                progress?.Report(MapProgress(p));
            }),
            cancellationToken).ConfigureAwait(false);

        if (!downloadResult.Success)
        {
            return ServerInstallationResult.Failed(downloadResult.Error ?? "Download failed");
        }

        progress?.Report(OfficialInstallProgress.Progress("Configuring", "Configuring server...", 95));

        // Config files (config.json, bans.json, etc.) are created by the Hytale server on first run

        progress?.Report(OfficialInstallProgress.Completed());

        return ServerInstallationResult.Succeeded(downloadResult.Version!, server.InstallPath);
    }

    public async Task<ServerInstallationResult> UpdateServerAsync(
        GameServer server,
        string targetVersion,
        IProgress<OfficialInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // For Hytale, update is essentially a re-download
        return await InstallServerAsync(server, targetVersion, progress, cancellationToken)
            .ConfigureAwait(false);
    }

    private static (string version, string patchline) ParseVersionString(string versionString)
    {
        var parts = versionString.Split(':');
        if (parts.Length == 2)
        {
            return (parts[0], parts[1]);
        }
        // Default to release if no patchline specified
        return (versionString, "release");
    }

    private static OfficialInstallProgress MapProgress(HytaleDownloadProgress p)
    {
        return new OfficialInstallProgress
        {
            Phase = p.Phase.ToString(),
            Message = p.Message,
            Percentage = p.Percentage,
            BytesDownloaded = p.BytesDownloaded,
            TotalBytes = p.TotalBytes
        };
    }

    private static Dictionary<string, ConfigurationSchema> GetConfigurationSchemas()
    {
        // Hytale server configs are in the Server/ subfolder, not the root install path
        return new Dictionary<string, ConfigurationSchema>
        {
            ["Server/config.json"] = new ConfigurationSchema
            {
                FileName = "Server/config.json",
                DisplayName = "Server Configuration",
                Description = "Main server configuration",
                FileType = ConfigFileType.Json,
                Sections =
                [
                    new ConfigSection
                    {
                        Key = "general",
                        DisplayName = "General Settings",
                        Description = "Basic server configuration"
                    },
                    new ConfigSection
                    {
                        Key = "gameplay",
                        DisplayName = "Gameplay",
                        Description = "Gameplay and world settings"
                    },
                    new ConfigSection
                    {
                        Key = "performance",
                        DisplayName = "Performance",
                        Description = "Performance and networking settings"
                    }
                ],
                Fields =
                [
                    // General Settings
                    new ConfigField
                    {
                        Key = "ServerName",
                        DisplayName = "Server Name",
                        Description = "The name displayed in the server browser.",
                        Type = ConfigFieldType.String,
                        DefaultValue = "Hytale Server",
                        Category = "general",
                        Validation = new ValidationRule { MaxLength = 64 }
                    },
                    new ConfigField
                    {
                        Key = "MOTD",
                        DisplayName = "Message of the Day",
                        Description = "Server description shown to players.",
                        Type = ConfigFieldType.Text,
                        DefaultValue = "",
                        Category = "general",
                        Validation = new ValidationRule { MaxLength = 256 }
                    },
                    new ConfigField
                    {
                        Key = "Password",
                        DisplayName = "Server Password",
                        Description = "Password required to join. Leave empty for no password.",
                        Type = ConfigFieldType.Password,
                        DefaultValue = "",
                        Category = "general"
                    },
                    new ConfigField
                    {
                        Key = "MaxPlayers",
                        DisplayName = "Max Players",
                        Description = "Maximum number of players allowed on the server.",
                        Type = ConfigFieldType.Slider,
                        DefaultValue = 100,
                        Category = "general",
                        Validation = new ValidationRule { MinValue = 1, MaxValue = 1000 }
                    },
                    // Gameplay Settings
                    new ConfigField
                    {
                        Key = "Defaults.World",
                        JsonPath = "Defaults.World",
                        DisplayName = "Default World",
                        Description = "The world players spawn in by default.",
                        Type = ConfigFieldType.String,
                        DefaultValue = "default",
                        Category = "gameplay"
                    },
                    new ConfigField
                    {
                        Key = "Defaults.GameMode",
                        JsonPath = "Defaults.GameMode",
                        DisplayName = "Default Game Mode",
                        Description = "The game mode for new players.",
                        Type = ConfigFieldType.Select,
                        DefaultValue = "Adventure",
                        Category = "gameplay",
                        Options =
                        [
                            new SelectOption { Label = "Adventure", Value = "Adventure" },
                            new SelectOption { Label = "Creative", Value = "Creative" },
                            // new SelectOption { Label = "Exploration", Value = "Exploration" }
                        ]
                    },
                    // Performance Settings
                    new ConfigField
                    {
                        Key = "MaxViewRadius",
                        DisplayName = "Max View Radius",
                        Description = "Maximum view distance in chunks.",
                        Type = ConfigFieldType.Slider,
                        DefaultValue = 32,
                        Category = "performance",
                        Validation = new ValidationRule { MinValue = 4, MaxValue = 64 }
                    },
                    new ConfigField
                    {
                        Key = "LocalCompressionEnabled",
                        DisplayName = "Local Compression",
                        Description = "Enable compression for local connections.",
                        Type = ConfigFieldType.Bool,
                        DefaultValue = false,
                        Category = "performance"
                    }
                ]
            },
            ["Server/bans.json"] = new ConfigurationSchema
            {
                FileName = "Server/bans.json",
                DisplayName = "Bans",
                Description = "Banned players list (edit via raw editor)",
                FileType = ConfigFileType.Json,
                Sections = [],
                Fields = []
            },
            ["Server/permissions.json"] = new ConfigurationSchema
            {
                FileName = "Server/permissions.json",
                DisplayName = "Permissions",
                Description = "Player permissions configuration (edit via raw editor)",
                FileType = ConfigFileType.Json,
                Sections = [],
                Fields = []
            },
            ["Server/whitelist.json"] = new ConfigurationSchema
            {
                FileName = "Server/whitelist.json",
                DisplayName = "Whitelist",
                Description = "Whitelisted players (edit via raw editor)",
                FileType = ConfigFileType.Json,
                Sections = [],
                Fields = []
            }
        };
    }
}
