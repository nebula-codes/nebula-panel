using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.OfficialGames.ArkSurvivalAscended;

/// <summary>
/// Handles ASA server installation and updates via SteamCMD.
/// </summary>
public class ArkServerInstaller(
    ISteamCmdService steamCmdService,
    ILogger<ArkServerInstaller> logger)
{
    private readonly ISteamCmdService _steamCmdService = steamCmdService;
    private readonly ILogger<ArkServerInstaller> _logger = logger;

    private const string ConfigRelativePath = "ShooterGame/Saved/Config/WindowsServer";

    /// <summary>
    /// The path inside the acekorneya/asa_server container where the game is installed
    /// (Wine prefix → SteamCMD → ARK Dedicated Server).
    /// </summary>
    private const string DockerGamePath =
        "/usr/games/.wine/drive_c/POK/Steam/steamapps/common/ARK Survival Ascended Dedicated Server";

    /// <summary>
    /// Installs ASA server to the specified location.
    /// </summary>
    public async Task<ServerInstallationResult> InstallAsync(
        GameServer server,
        string version,
        IProgress<OfficialInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var isDocker = server.DeploymentType == ServerDeploymentType.Docker;

        if (isDocker)
        {
            return await InstallDockerAsync(server, version, progress, cancellationToken)
                .ConfigureAwait(false);
        }

        return await InstallNativeAsync(server, version, progress, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Docker install: creates host directories, default config files, and configures Docker settings.
    /// The Docker image (acekorneya/asa_server) already contains the server.
    /// </summary>
    private async Task<ServerInstallationResult> InstallDockerAsync(
        GameServer server,
        string version,
        IProgress<OfficialInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            progress?.Report(OfficialInstallProgress.Progress("Preparing", "Creating installation directory...", 10));
            Directory.CreateDirectory(server.InstallPath);

            progress?.Report(OfficialInstallProgress.Progress("Configuring", "Generating default configuration...", 30));
            await GenerateDefaultConfigAsync(server.InstallPath, cancellationToken).ConfigureAwait(false);

            progress?.Report(OfficialInstallProgress.Progress("Configuring", "Creating data directories...", 60));
            CreateDataDirectories(server.InstallPath);

            // Configure Docker settings for the acekorneya/asa_server image
            progress?.Report(OfficialInstallProgress.Progress("Configuring", "Setting up Docker configuration...", 80));
            var gamePort = server.PrimaryPort > 0 ? server.PrimaryPort : 7777;

            server.DockerConfig = new DockerConfiguration
            {
                Image = "acekorneya/asa_server",
                Tag = "latest",
                RunAsRoot = true, // Image init script modifies /etc/localtime, /etc/group
                Ports =
                [
                    new PortMapping { HostPort = gamePort, ContainerPort = gamePort, Protocol = "udp" },
                    new PortMapping { HostPort = gamePort + 1, ContainerPort = gamePort + 1, Protocol = "udp" },
                    new PortMapping { HostPort = 27015, ContainerPort = 27015, Protocol = "udp" },
                    new PortMapping { HostPort = 27020, ContainerPort = 27020, Protocol = "tcp" }
                ],
                Volumes =
                [
                    new VolumeMount { HostPath = server.InstallPath, ContainerPath = DockerGamePath }
                ],
                EnvironmentVariables = new Dictionary<string, string>
                {
                    ["PUID"] = "1000",
                    ["PGID"] = "1000",
                    ["BATTLEEYE"] = "FALSE"
                },
                Tty = false
            };

            progress?.Report(OfficialInstallProgress.Completed());

            _logger.LogInformation("Successfully set up Docker ASA server at {Path}", server.InstallPath);
            return ServerInstallationResult.Succeeded(version, server.InstallPath);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ASA Docker installation was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set up Docker ASA server");
            return ServerInstallationResult.Failed($"Installation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Native install: downloads ASA server via SteamCMD, generates config, creates data directories.
    /// </summary>
    private async Task<ServerInstallationResult> InstallNativeAsync(
        GameServer server,
        string version,
        IProgress<OfficialInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        try
        {
            // Phase 1: Ensure SteamCMD is available
            progress?.Report(OfficialInstallProgress.Progress("Preparing", "Checking SteamCMD installation...", 5));

            var steamCmdProgress = new Progress<SteamCmdProgress>(p =>
            {
                var percentage = 5 + (p.PercentComplete * 0.1); // 5-15%
                progress?.Report(OfficialInstallProgress.Progress("SteamCMD", p.Message, percentage));
            });

            var steamCmdReady = await _steamCmdService.EnsureInstalledAsync(steamCmdProgress, cancellationToken)
                .ConfigureAwait(false);

            if (!steamCmdReady)
            {
                _logger.LogError("Failed to install SteamCMD");
                return ServerInstallationResult.Failed("Failed to install SteamCMD");
            }

            // Phase 2: Create installation directory
            progress?.Report(OfficialInstallProgress.Progress("Preparing", "Creating installation directory...", 15));
            Directory.CreateDirectory(server.InstallPath);

            // Phase 3: Download ASA server via SteamCMD
            progress?.Report(OfficialInstallProgress.Progress("Downloading", "Downloading ASA server via Steam...", 20));

            var downloadProgress = new Progress<SteamCmdProgress>(p =>
            {
                var percentage = 20 + (p.PercentComplete * 0.6); // 20-80%
                var message = p.State switch
                {
                    SteamCmdProgressState.DownloadingGame => $"Downloading ASA server... {p.PercentComplete:F1}%",
                    SteamCmdProgressState.Validating => "Validating files...",
                    SteamCmdProgressState.Installing => "Installing files...",
                    _ => p.Message
                };
                progress?.Report(OfficialInstallProgress.Progress("Downloading", message, percentage));
            });

            var installSuccess = await _steamCmdService.InstallOrUpdateGameAsync(
                ArkSurvivalAscendedProvider.AsaServerAppId,
                server.InstallPath,
                null,  // No branch
                null,  // No beta password
                true,  // Validate files
                downloadProgress,
                cancellationToken
            ).ConfigureAwait(false);

            if (!installSuccess)
            {
                _logger.LogError("SteamCMD installation failed for ASA");
                return ServerInstallationResult.Failed("Failed to download ASA server via SteamCMD");
            }

            // Phase 4: Generate default configuration
            progress?.Report(OfficialInstallProgress.Progress("Configuring", "Generating default configuration...", 85));
            await GenerateDefaultConfigAsync(server.InstallPath, cancellationToken).ConfigureAwait(false);

            // Phase 5: Create data directories
            progress?.Report(OfficialInstallProgress.Progress("Configuring", "Creating data directories...", 95));
            CreateDataDirectories(server.InstallPath);

            // Done
            progress?.Report(OfficialInstallProgress.Completed());

            var installedVersion = version == "latest"
                ? await GetInstalledVersionAsync(server.InstallPath, cancellationToken)
                : version;

            _logger.LogInformation("Successfully installed ASA server {Version} at {Path}",
                installedVersion, server.InstallPath);

            return ServerInstallationResult.Succeeded(installedVersion, server.InstallPath);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ASA server installation was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install ASA server");
            return ServerInstallationResult.Failed($"Installation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates an existing ASA server to a new version.
    /// </summary>
    public async Task<ServerInstallationResult> UpdateAsync(
        GameServer server,
        string targetVersion,
        IProgress<OfficialInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!Directory.Exists(server.InstallPath))
            {
                _logger.LogError("Server installation not found at {Path}", server.InstallPath);
                return ServerInstallationResult.Failed("Server installation not found");
            }

            progress?.Report(OfficialInstallProgress.Progress("Updating", "Checking for updates...", 5));

            // Backup config files before update
            var configDir = Path.Combine(server.InstallPath, ConfigRelativePath);
            var backupFiles = new[] { "GameUserSettings.ini", "Game.ini" };
            var backups = new Dictionary<string, string>();

            foreach (var file in backupFiles)
            {
                var filePath = Path.Combine(configDir, file);
                if (File.Exists(filePath))
                {
                    backups[file] = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
                }
            }

            // Use SteamCMD to update
            progress?.Report(OfficialInstallProgress.Progress("Downloading", "Downloading update via Steam...", 15));

            var downloadProgress = new Progress<SteamCmdProgress>(p =>
            {
                var percentage = 15 + (p.PercentComplete * 0.7); // 15-85%
                progress?.Report(OfficialInstallProgress.Progress("Downloading", p.Message, percentage));
            });

            var updateSuccess = await _steamCmdService.InstallOrUpdateGameAsync(
                ArkSurvivalAscendedProvider.AsaServerAppId,
                server.InstallPath,
                null,
                null,
                true,
                downloadProgress,
                cancellationToken
            ).ConfigureAwait(false);

            if (!updateSuccess)
            {
                _logger.LogError("SteamCMD update failed for ASA");
                return ServerInstallationResult.Failed("Failed to update ASA server via SteamCMD");
            }

            // Restore backed up config files
            progress?.Report(OfficialInstallProgress.Progress("Configuring", "Restoring configuration...", 90));

            foreach (var backup in backups)
            {
                var filePath = Path.Combine(configDir, backup.Key);
                await File.WriteAllTextAsync(filePath, backup.Value, cancellationToken).ConfigureAwait(false);
            }

            progress?.Report(OfficialInstallProgress.Completed());

            var installedVersion = targetVersion == "latest"
                ? await GetInstalledVersionAsync(server.InstallPath, cancellationToken)
                : targetVersion;

            _logger.LogInformation("Successfully updated ASA server to {Version} at {Path}",
                installedVersion, server.InstallPath);

            return ServerInstallationResult.Succeeded(installedVersion, server.InstallPath);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ASA server update was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update ASA server");
            return ServerInstallationResult.Failed($"Update failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates default GameUserSettings.ini and Game.ini if missing.
    /// </summary>
    private async Task GenerateDefaultConfigAsync(string installPath, CancellationToken cancellationToken)
    {
        var configDir = Path.Combine(installPath, ConfigRelativePath);
        Directory.CreateDirectory(configDir);

        // Generate GameUserSettings.ini
        var gameUserSettingsPath = Path.Combine(configDir, "GameUserSettings.ini");
        if (!File.Exists(gameUserSettingsPath) || new FileInfo(gameUserSettingsPath).Length == 0)
        {
            var defaultGameUserSettings = """
                [ServerSettings]
                ServerAdminPassword=ChangeMe
                RCONEnabled=True
                RCONPort=27020
                MaxPlayers=70
                DifficultyOffset=1.000000
                ServerPVE=False
                AllowFlyerCarryPVE=False
                DisableStructureDecayPvE=False
                AllowThirdPersonPlayer=True
                ShowMapPlayerLocation=True
                EnableCrossplay=False
                TamingSpeedMultiplier=1.000000
                HarvestAmountMultiplier=1.000000
                XPMultiplier=1.000000
                MatingIntervalMultiplier=1.000000
                BabyMatureSpeedMultiplier=1.000000
                EggHatchSpeedMultiplier=1.000000
                HarvestHealthMultiplier=1.000000
                PlayerDamageMultiplier=1.000000
                DinoDamageMultiplier=1.000000
                PlayerResistanceMultiplier=1.000000
                DinoResistanceMultiplier=1.000000
                ResourcesRespawnPeriodMultiplier=1.000000
                DayTimeSpeedScale=1.000000
                NightTimeSpeedScale=1.000000
                DayCycleSpeedScale=1.000000
                AutoSavePeriodMinutes=15.000000
                MaxTamedDinos=5000
                ClampItemStats=False
                AdminLogging=True
                TribeLogDestroyedEnemyStructures=False

                [SessionSettings]
                SessionName=Nebula Panel ASA Server
                QueryPort=27015
                Port=7777
                """;

            await File.WriteAllTextAsync(gameUserSettingsPath, defaultGameUserSettings, cancellationToken)
                .ConfigureAwait(false);
            _logger.LogDebug("Generated default GameUserSettings.ini at {Path}", gameUserSettingsPath);
        }

        // Generate Game.ini
        var gameIniPath = Path.Combine(configDir, "Game.ini");
        if (!File.Exists(gameIniPath) || new FileInfo(gameIniPath).Length == 0)
        {
            var defaultGameIni = """
                [/Script/ShooterGame.ShooterGameMode]
                bAutoUnlockAllEngrams=False
                MaxNumberOfPlayersInTribe=0
                BabyImprintingStatScaleMultiplier=1.000000
                BabyCuddleIntervalMultiplier=1.000000
                """;

            await File.WriteAllTextAsync(gameIniPath, defaultGameIni, cancellationToken)
                .ConfigureAwait(false);
            _logger.LogDebug("Generated default Game.ini at {Path}", gameIniPath);
        }
    }

    /// <summary>
    /// Creates ASA data directories (Mods, Saved, etc.).
    /// </summary>
    private void CreateDataDirectories(string installPath)
    {
        Directory.CreateDirectory(Path.Combine(installPath, "ShooterGame", "Content", "Mods"));
        Directory.CreateDirectory(Path.Combine(installPath, "ShooterGame", "Saved", "SavedArks"));
        Directory.CreateDirectory(Path.Combine(installPath, "ShooterGame", "Saved", "Logs"));
        Directory.CreateDirectory(Path.Combine(installPath, ConfigRelativePath));
    }

    /// <summary>
    /// Gets the installed version from Steam app info.
    /// </summary>
    private async Task<string> GetInstalledVersionAsync(string installPath, CancellationToken cancellationToken)
    {
        var appInfo = await _steamCmdService.GetAppInfoAsync(
            ArkSurvivalAscendedProvider.AsaServerAppId,
            installPath,
            cancellationToken
        ).ConfigureAwait(false);

        if (appInfo?.BuildId is not null)
        {
            return $"build-{appInfo.BuildId}";
        }

        return "latest";
    }
}
