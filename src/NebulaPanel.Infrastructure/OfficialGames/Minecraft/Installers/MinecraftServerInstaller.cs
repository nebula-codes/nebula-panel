using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;
using NebulaPanel.Infrastructure.OfficialGames.Minecraft.Models;

namespace NebulaPanel.Infrastructure.OfficialGames.Minecraft.Installers;

/// <summary>
/// Main coordinator for Minecraft server installation.
/// Orchestrates the installation process across different loader types.
/// </summary>
public class MinecraftServerInstaller(
    IEnumerable<IMinecraftLoaderInstaller> loaderInstallers,
    MinecraftJavaDetector javaDetector,
    ILogger<MinecraftServerInstaller> logger)
{
    private readonly Dictionary<MinecraftLoader, IMinecraftLoaderInstaller> _installers =
        loaderInstallers.ToDictionary(i => i.LoaderType);
    private readonly MinecraftJavaDetector _javaDetector = javaDetector;
    private readonly ILogger<MinecraftServerInstaller> _logger = logger;

    /// <summary>
    /// Installs a Minecraft server with the specified version.
    /// </summary>
    /// <param name="server">The game server entity to install.</param>
    /// <param name="version">Version string (e.g., "paper-1.20.4-496").</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Installation result.</returns>
    public async Task<ServerInstallationResult> InstallAsync(
        GameServer server,
        string version,
        IProgress<OfficialInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Minecraft server installation: {Version} at {Path}",
            version, server.InstallPath);

        progress?.Report(OfficialInstallProgress.Starting("Preparing"));

        // 1. Parse version string
        var parsedVersion = MinecraftVersionParser.Parse(version);
        if (parsedVersion is null)
        {
            return ServerInstallationResult.Failed(
                $"Invalid version string: {version}. Expected format like 'paper-1.20.4-496' or 'vanilla-1.20.4'");
        }

        _logger.LogDebug("Parsed version: Loader={Loader}, MC={MC}, LoaderVer={LoaderVer}",
            parsedVersion.LoaderType, parsedVersion.MinecraftVersion, parsedVersion.LoaderVersion);

        // 2. Find appropriate installer
        if (!_installers.TryGetValue(parsedVersion.LoaderType, out var installer))
        {
            return ServerInstallationResult.Failed(
                $"No installer available for loader type: {parsedVersion.LoaderType}");
        }

        // 3. Determine Java requirements
        var requiredJavaVersion = MinecraftJavaDetector.GetRequiredJavaVersion(parsedVersion.MinecraftVersion);
        var configuredJavaPath = server.NativeConfig?.JavaPath;
        var javaPath = _javaDetector.ResolveJavaPath(configuredJavaPath);

        _logger.LogDebug("Java requirements: Required={Required}, Path={Path}",
            requiredJavaVersion, javaPath);

        // 4. Validate Java version
        progress?.Report(OfficialInstallProgress.Progress("Preparing", "Validating Java installation...", 1));

        var javaValidation = await _javaDetector.ValidateJavaAsync(
            javaPath, requiredJavaVersion, cancellationToken).ConfigureAwait(false);

        if (!javaValidation.IsValid)
        {
            _logger.LogWarning("Java validation failed: {Error}", javaValidation.Error);
            return ServerInstallationResult.Failed(javaValidation.Error!);
        }

        _logger.LogInformation("Java validation passed: Version {Version} at {Path}",
            javaValidation.DetectedVersion?.MajorVersion, javaValidation.DetectedVersion?.Path);

        // 5. Create install directory
        if (!Directory.Exists(server.InstallPath))
        {
            _logger.LogDebug("Creating install directory: {Path}", server.InstallPath);
            Directory.CreateDirectory(server.InstallPath);
        }

        // 6. Create installation context
        var context = new MinecraftInstallContext
        {
            Server = server,
            VersionString = version,
            LoaderType = parsedVersion.LoaderType,
            MinecraftVersion = parsedVersion.MinecraftVersion,
            LoaderVersion = parsedVersion.LoaderVersion,
            JavaPath = javaPath,
            RequiredJavaVersion = requiredJavaVersion
        };

        // 7. Run loader-specific installation
        var loaderResult = await installer.InstallAsync(context, progress, cancellationToken)
            .ConfigureAwait(false);

        if (!loaderResult.Success)
        {
            _logger.LogError("Loader installation failed: {Error}", loaderResult.Error);
            return ServerInstallationResult.Failed(loaderResult.Error!);
        }

        // 8. Create common configuration files
        progress?.Report(OfficialInstallProgress.Progress("Configuring", "Creating configuration files...", 96));

        await MinecraftConfigFiles.CreateEulaAsync(server.InstallPath, cancellationToken)
            .ConfigureAwait(false);

        var createdProperties = await MinecraftConfigFiles.CreateDefaultServerPropertiesAsync(
            server.InstallPath, server, cancellationToken).ConfigureAwait(false);

        if (createdProperties)
            _logger.LogDebug("Created default server.properties");

        // 9. Configure NativeConfiguration with startup command
        progress?.Report(OfficialInstallProgress.Progress("Configuring", "Configuring startup command...", 98));

        ConfigureStartupCommand(server, context, installer);

        // 10. Update server entity
        server.InstalledVersion = version;
        server.Status = ServerStatus.Stopped;

        progress?.Report(OfficialInstallProgress.Completed());

        _logger.LogInformation("Successfully installed Minecraft server: {Version}", version);

        return ServerInstallationResult.Succeeded(version, server.InstallPath);
    }

    /// <summary>
    /// Updates an existing Minecraft server to a new version.
    /// </summary>
    /// <param name="server">The game server entity to update.</param>
    /// <param name="targetVersion">Target version string.</param>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Update result.</returns>
    public async Task<ServerInstallationResult> UpdateAsync(
        GameServer server,
        string targetVersion,
        IProgress<OfficialInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting Minecraft server update: {CurrentVersion} -> {TargetVersion}",
            server.InstalledVersion, targetVersion);

        // For Minecraft, update is essentially a reinstall
        // World data and custom configs are preserved because we don't delete them

        // Check if server is stopped
        if (server.Status != ServerStatus.Stopped)
        {
            return ServerInstallationResult.Failed(
                "Server must be stopped before updating. Current status: " + server.Status);
        }

        // Backup server.jar if it exists (in case update fails)
        var serverJarPath = Path.Combine(server.InstallPath, "server.jar");
        var backupJarPath = Path.Combine(server.InstallPath, "server.jar.backup");

        if (File.Exists(serverJarPath))
        {
            _logger.LogDebug("Backing up existing server.jar");
            File.Copy(serverJarPath, backupJarPath, overwrite: true);
        }

        try
        {
            var result = await InstallAsync(server, targetVersion, progress, cancellationToken)
                .ConfigureAwait(false);

            // Clean up backup on success
            if (result.Success && File.Exists(backupJarPath))
            {
                try { File.Delete(backupJarPath); }
                catch { /* ignore */ }
            }

            return result;
        }
        catch
        {
            // Restore backup on failure
            if (File.Exists(backupJarPath))
            {
                _logger.LogWarning("Update failed, restoring backup server.jar");
                try { File.Copy(backupJarPath, serverJarPath, overwrite: true); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to restore server.jar backup from {BackupPath}", backupJarPath);
                }
            }
            throw;
        }
    }

    private void ConfigureStartupCommand(
        GameServer server,
        MinecraftInstallContext context,
        IMinecraftLoaderInstaller installer)
    {
        var startupInfo = installer.GetStartupCommand(context);

        server.NativeConfig ??= new NativeConfiguration();
        server.NativeConfig.WorkingDirectory = context.InstallPath;
        server.NativeConfig.JavaPath = context.JavaPath;

        if (startupInfo.UseArgsFile)
        {
            // Modern Forge uses @file syntax - Java runs directly with args
            server.NativeConfig.ExecutablePath = string.Empty;
            server.NativeConfig.Arguments = startupInfo.Arguments;
        }
        else if (startupInfo.ExecutablePath is not null)
        {
            // Standard jar execution
            server.NativeConfig.ExecutablePath = startupInfo.ExecutablePath;
            server.NativeConfig.Arguments = startupInfo.Arguments;
        }

        // Set default JVM arguments if not already set
        if (string.IsNullOrEmpty(server.NativeConfig.JavaArguments))
        {
            var maxMemory = server.ResourceLimits?.MaxMemoryMb ?? 2048;
            var minMemory = Math.Min(1024, maxMemory);

            // Use Aikar's flags for Paper/Purpur/Spigot, basic flags otherwise
            if (context.LoaderType is MinecraftLoader.Paper or MinecraftLoader.Purpur or MinecraftLoader.Spigot)
            {
                server.NativeConfig.JavaArguments = BuildAikarsFlags(minMemory, maxMemory);
            }
            else
            {
                server.NativeConfig.JavaArguments = $"-Xms{minMemory}M -Xmx{maxMemory}M";
            }
        }

        _logger.LogDebug("Configured startup: Exe={Exe}, Args={Args}, JvmArgs={Jvm}",
            server.NativeConfig.ExecutablePath,
            server.NativeConfig.Arguments,
            server.NativeConfig.JavaArguments);
    }

    /// <summary>
    /// Builds Aikar's recommended JVM flags for Minecraft servers.
    /// https://docs.papermc.io/paper/aikars-flags
    /// </summary>
    private static string BuildAikarsFlags(int minMemoryMb, int maxMemoryMb)
    {
        // Simplified Aikar's flags - the full set is quite long
        return $"-Xms{minMemoryMb}M -Xmx{maxMemoryMb}M " +
               "-XX:+UseG1GC -XX:+ParallelRefProcEnabled " +
               "-XX:MaxGCPauseMillis=200 -XX:+UnlockExperimentalVMOptions " +
               "-XX:+DisableExplicitGC -XX:+AlwaysPreTouch " +
               "-XX:G1NewSizePercent=30 -XX:G1MaxNewSizePercent=40 " +
               "-XX:G1HeapRegionSize=8M -XX:G1ReservePercent=20 " +
               "-XX:G1HeapWastePercent=5 -XX:G1MixedGCCountTarget=4 " +
               "-XX:InitiatingHeapOccupancyPercent=15 " +
               "-XX:G1MixedGCLiveThresholdPercent=90 " +
               "-XX:G1RSetUpdatingPauseTimePercent=5 " +
               "-XX:SurvivorRatio=32 -XX:+PerfDisableSharedMem " +
               "-XX:MaxTenuringThreshold=1 -Dusing.aikars.flags=https://mcflags.emc.gs";
    }
}
