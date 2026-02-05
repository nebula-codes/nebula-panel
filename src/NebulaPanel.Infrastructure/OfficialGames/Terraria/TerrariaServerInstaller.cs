using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.OfficialGames.Terraria;

/// <summary>
/// Handles tModLoader server installation and updates via SteamCMD.
/// </summary>
public class TerrariaServerInstaller(
    ISteamCmdService steamCmdService,
    ILogger<TerrariaServerInstaller> logger)
{
    private readonly ISteamCmdService _steamCmdService = steamCmdService;
    private readonly ILogger<TerrariaServerInstaller> _logger = logger;

    /// <summary>
    /// Installs tModLoader server to the specified location.
    /// </summary>
    public async Task<ServerInstallationResult> InstallAsync(
        GameServer server,
        string version,
        IProgress<OfficialInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var isDocker = server.DeploymentType == ServerDeploymentType.Docker;

        // Docker images already contain tModLoader — skip SteamCMD and only set up
        // the host-side config file and data directories that get mounted into the container.
        if (isDocker)
        {
            return await InstallDockerAsync(server, version, progress, cancellationToken)
                .ConfigureAwait(false);
        }

        return await InstallNativeAsync(server, version, progress, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Docker install: creates config file and data directories only.
    /// The Docker image (jacobsmile/tmodloader1.4) already contains tModLoader.
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
            await GenerateDefaultConfigAsync(server.InstallPath, isDocker: true, cancellationToken).ConfigureAwait(false);

            progress?.Report(OfficialInstallProgress.Progress("Configuring", "Creating data directories...", 60));
            await CreateDataDirectoriesAsync(server.InstallPath, isDocker: true, cancellationToken).ConfigureAwait(false);

            progress?.Report(OfficialInstallProgress.Completed());

            _logger.LogInformation("Successfully set up Docker tModLoader server at {Path}", server.InstallPath);
            return ServerInstallationResult.Succeeded(version, server.InstallPath);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("tModLoader Docker installation was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set up Docker tModLoader server");
            return ServerInstallationResult.Failed($"Installation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Native install: downloads tModLoader via SteamCMD, generates config, creates data directories.
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

            // Phase 3: Download tModLoader via SteamCMD
            progress?.Report(OfficialInstallProgress.Progress("Downloading", "Downloading tModLoader via Steam...", 20));

            var downloadProgress = new Progress<SteamCmdProgress>(p =>
            {
                var percentage = 20 + (p.PercentComplete * 0.6); // 20-80%
                var message = p.State switch
                {
                    SteamCmdProgressState.DownloadingGame => $"Downloading tModLoader... {p.PercentComplete:F1}%",
                    SteamCmdProgressState.Validating => "Validating files...",
                    SteamCmdProgressState.Installing => "Installing files...",
                    _ => p.Message
                };
                progress?.Report(OfficialInstallProgress.Progress("Downloading", message, percentage));
            });

            // Determine branch based on version
            string? branch = version != "latest" ? GetBranchForVersion(version) : null;

            var installSuccess = await _steamCmdService.InstallOrUpdateGameAsync(
                TerrariaProvider.TModLoaderAppId,
                server.InstallPath,
                branch,
                null, // No beta password
                true, // Validate files
                downloadProgress,
                cancellationToken
            ).ConfigureAwait(false);

            if (!installSuccess)
            {
                _logger.LogError("SteamCMD installation failed for tModLoader");
                return ServerInstallationResult.Failed("Failed to download tModLoader via SteamCMD");
            }

            // Phase 4: Generate default configuration
            progress?.Report(OfficialInstallProgress.Progress("Configuring", "Generating default configuration...", 85));

            await GenerateDefaultConfigAsync(server.InstallPath, isDocker: false, cancellationToken).ConfigureAwait(false);

            // Phase 5: Set executable permissions on Linux
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                progress?.Report(OfficialInstallProgress.Progress("Configuring", "Setting file permissions...", 90));
                await SetExecutablePermissionsAsync(server.InstallPath, cancellationToken).ConfigureAwait(false);
            }

            // Phase 6: Create data directories
            progress?.Report(OfficialInstallProgress.Progress("Configuring", "Creating data directories...", 95));
            await CreateDataDirectoriesAsync(server.InstallPath, isDocker: false, cancellationToken).ConfigureAwait(false);

            // Done
            progress?.Report(OfficialInstallProgress.Completed());

            var installedVersion = version == "latest" ? await GetInstalledVersionAsync(server.InstallPath, cancellationToken) : version;

            _logger.LogInformation("Successfully installed tModLoader {Version} at {Path}",
                installedVersion, server.InstallPath);

            return ServerInstallationResult.Succeeded(installedVersion, server.InstallPath);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("tModLoader installation was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install tModLoader server");
            return ServerInstallationResult.Failed($"Installation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Updates an existing tModLoader server to a new version.
    /// </summary>
    public async Task<ServerInstallationResult> UpdateAsync(
        GameServer server,
        string targetVersion,
        IProgress<OfficialInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Verify installation exists
            if (!Directory.Exists(server.InstallPath))
            {
                _logger.LogError("Server installation not found at {Path}", server.InstallPath);
                return ServerInstallationResult.Failed("Server installation not found");
            }

            progress?.Report(OfficialInstallProgress.Progress("Updating", "Checking for updates...", 5));

            // Backup important files before update
            var backupFiles = new[] { "serverconfig.txt", "banlist.txt" };
            var backups = new Dictionary<string, string>();

            foreach (var file in backupFiles)
            {
                var filePath = Path.Combine(server.InstallPath, file);
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

            string? branch = targetVersion != "latest" ? GetBranchForVersion(targetVersion) : null;

            var updateSuccess = await _steamCmdService.InstallOrUpdateGameAsync(
                TerrariaProvider.TModLoaderAppId,
                server.InstallPath,
                branch,
                null,
                true,
                downloadProgress,
                cancellationToken
            ).ConfigureAwait(false);

            if (!updateSuccess)
            {
                _logger.LogError("SteamCMD update failed for tModLoader");
                return ServerInstallationResult.Failed("Failed to update tModLoader via SteamCMD");
            }

            // Restore backed up files
            progress?.Report(OfficialInstallProgress.Progress("Configuring", "Restoring configuration...", 90));

            foreach (var backup in backups)
            {
                var filePath = Path.Combine(server.InstallPath, backup.Key);
                await File.WriteAllTextAsync(filePath, backup.Value, cancellationToken).ConfigureAwait(false);
            }

            // Set permissions on Linux
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                progress?.Report(OfficialInstallProgress.Progress("Configuring", "Setting file permissions...", 95));
                await SetExecutablePermissionsAsync(server.InstallPath, cancellationToken).ConfigureAwait(false);
            }

            progress?.Report(OfficialInstallProgress.Completed());

            var installedVersion = targetVersion == "latest"
                ? await GetInstalledVersionAsync(server.InstallPath, cancellationToken)
                : targetVersion;

            _logger.LogInformation("Successfully updated tModLoader to {Version} at {Path}",
                installedVersion, server.InstallPath);

            return ServerInstallationResult.Succeeded(installedVersion, server.InstallPath);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("tModLoader update was cancelled");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update tModLoader server");
            return ServerInstallationResult.Failed($"Update failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates a default serverconfig.txt file.
    /// </summary>
    /// <param name="installPath">Server installation directory.</param>
    /// <param name="isDocker">Whether this is a Docker deployment. Docker paths are absolute
    /// inside the container; native paths are relative to the working directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task GenerateDefaultConfigAsync(string installPath, bool isDocker, CancellationToken cancellationToken)
    {
        var configPath = Path.Combine(installPath, "serverconfig.txt");

        // Don't overwrite existing config (but do overwrite empty placeholder files
        // created by EnsureBindMountFilesExist when the real config didn't exist yet)
        if (File.Exists(configPath) && new FileInfo(configPath).Length > 0)
        {
            _logger.LogDebug("Configuration file already exists, skipping generation");
            return;
        }

        // Docker: the host directory is mounted to /data, and tModLoader expects data
        // under /data/tModLoader/. Use absolute container paths.
        // Native: the server's working directory is set to the install path by the executor,
        // so relative paths resolve correctly and stay portable.
        var worldPath = isDocker ? "/data/tModLoader/Worlds" : "Worlds";
        var modPath = isDocker ? "/data/tModLoader/Mods" : "Mods";

        var defaultConfig = $"""
            # tModLoader Server Configuration
            # Generated by Nebula Panel

            # World Settings
            autocreate=3
            worldname=World
            difficulty=0

            # Data paths
            worldpath={worldPath}
            modpath={modPath}

            # Server Settings
            maxplayers=8
            port=7777
            password=
            motd=Welcome to the server!
            secure=1
            npcstream=60
            priority=1
            upnp=0
            steam=1
            lobby=friends
            language=en-US

            # Journey Mode Permissions (0=Locked, 1=Host Only, 2=All Players)
            journeypermission_time_setfrozen=2
            journeypermission_time_setdawn=2
            journeypermission_time_setspeed=2
            journeypermission_godmode=2
            journeypermission_wind_setstrength=2
            journeypermission_rain_setstrength=2
            journeypermission_setdifficulty=2
            journeypermission_biomespread_setfrozen=2
            journeypermission_setspawnrate=2
            journeypermission_increaseplacementrange=2
            """;

        await File.WriteAllTextAsync(configPath, defaultConfig, cancellationToken).ConfigureAwait(false);
        _logger.LogDebug("Generated default serverconfig.txt at {Path}", configPath);
    }

    /// <summary>
    /// Creates tModLoader data directories (Mods, Worlds, ModConfigs) and seed files.
    /// </summary>
    private async Task CreateDataDirectoriesAsync(string installPath, bool isDocker, CancellationToken cancellationToken)
    {
        // For Docker: the host directory is mounted to /data inside the container.
        // The tModLoader image expects data under /data/tModLoader/{Mods,Worlds,ModConfigs}.
        // For native: directories are relative to the install path (working directory).
        var dataBase = isDocker
            ? Path.Combine(installPath, "tModLoader")
            : installPath;

        var modsPath = Path.Combine(dataBase, "Mods");
        var worldsPath = Path.Combine(dataBase, "Worlds");
        var modConfigsPath = Path.Combine(dataBase, "ModConfigs");

        Directory.CreateDirectory(modsPath);
        Directory.CreateDirectory(worldsPath);
        Directory.CreateDirectory(modConfigsPath);

        // Create empty enabled.json if it doesn't exist
        var enabledJsonPath = Path.Combine(modsPath, "enabled.json");
        if (!File.Exists(enabledJsonPath))
        {
            await File.WriteAllTextAsync(enabledJsonPath, "[]", cancellationToken).ConfigureAwait(false);
        }

        // Create install.txt for workshop mod IDs
        var installTxtPath = Path.Combine(modsPath, "install.txt");
        if (!File.Exists(installTxtPath))
        {
            await File.WriteAllTextAsync(installTxtPath, "# Add Steam Workshop mod IDs here, one per line\n", cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Sets executable permissions on Linux.
    /// </summary>
    private async Task SetExecutablePermissionsAsync(string installPath, CancellationToken cancellationToken)
    {
        var executables = new[]
        {
            "start-tModLoaderServer.sh",
            "tModLoader",
            "tModLoader.bin.x86_64",
            "LaunchUtils/ScriptCaller.sh"
        };

        foreach (var executable in executables)
        {
            var exePath = Path.Combine(installPath, executable);
            if (File.Exists(exePath))
            {
                try
                {
                    var chmod = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "chmod",
                        Arguments = $"+x \"{exePath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    });

                    if (chmod is not null)
                    {
                        await chmod.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to set executable permission on {File}", executable);
                }
            }
        }
    }

    /// <summary>
    /// Gets the installed version from the manifest or version file.
    /// </summary>
    private async Task<string> GetInstalledVersionAsync(string installPath, CancellationToken cancellationToken)
    {
        // Try to get version from Steam app info
        var appInfo = await _steamCmdService.GetAppInfoAsync(
            TerrariaProvider.TModLoaderAppId,
            installPath,
            cancellationToken
        ).ConfigureAwait(false);

        if (appInfo?.BuildId is not null)
        {
            return $"build-{appInfo.BuildId}";
        }

        return "latest";
    }

    /// <summary>
    /// Maps a version string to a Steam branch name if applicable.
    /// </summary>
    private static string? GetBranchForVersion(string version)
    {
        // tModLoader uses branches like "1.4-stable", "1.4-preview", etc.
        // For now, return null to use the default branch
        // This can be expanded based on tModLoader's actual branch structure
        if (version.Contains("preview", StringComparison.OrdinalIgnoreCase))
        {
            return "1.4-preview";
        }

        return null;
    }
}
