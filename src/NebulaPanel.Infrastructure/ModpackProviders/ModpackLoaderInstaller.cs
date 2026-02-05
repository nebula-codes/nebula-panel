using Microsoft.Extensions.Logging;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.ValueObjects;
using NebulaPanel.Infrastructure.OfficialGames.Minecraft.Installers;
using NebulaPanel.Infrastructure.OfficialGames.Minecraft.Models;

namespace NebulaPanel.Infrastructure.ModpackProviders;

/// <summary>
/// Helper class for installing mod loaders during modpack installation.
/// Wraps the existing Minecraft loader installers for modpack-specific use.
/// </summary>
public class ModpackLoaderInstaller(
    IEnumerable<IMinecraftLoaderInstaller> loaderInstallers,
    MinecraftJavaDetector javaDetector,
    ILogger<ModpackLoaderInstaller> logger)
{
    private readonly Dictionary<MinecraftLoader, IMinecraftLoaderInstaller> _installers =
        loaderInstallers.ToDictionary(i => i.LoaderType);
    private readonly MinecraftJavaDetector _javaDetector = javaDetector;
    private readonly ILogger<ModpackLoaderInstaller> _logger = logger;

    /// <summary>
    /// Installs the specified mod loader for a modpack.
    /// </summary>
    /// <param name="server">The game server to install the loader on.</param>
    /// <param name="loader">The mod loader type to install.</param>
    /// <param name="minecraftVersion">The Minecraft version.</param>
    /// <param name="loaderVersion">Optional specific loader version.</param>
    /// <param name="progress">Progress reporter.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if installation succeeded.</returns>
    public async Task<LoaderInstallResult> InstallLoaderAsync(
        GameServer server,
        ModLoaderType loader,
        string minecraftVersion,
        string? loaderVersion,
        IProgress<ModpackInstallProgress>? progress = null,
        CancellationToken ct = default)
    {
        if (loader == ModLoaderType.None)
        {
            _logger.LogInformation("No mod loader required, skipping loader installation");
            return LoaderInstallResult.Succeeded(null, minecraftVersion);
        }

        // Auto-remap Forge to NeoForge for MC 1.20.2+ (Forge team rebranded to NeoForge)
        var effectiveLoader = loader;
        if (loader == ModLoaderType.Forge && IsNeoForgeRequired(minecraftVersion))
        {
            _logger.LogInformation(
                "Forge is not available for MC {McVersion}, automatically using NeoForge instead",
                minecraftVersion);
            effectiveLoader = ModLoaderType.NeoForge;
        }

        var mcLoader = MapToMinecraftLoader(effectiveLoader);
        if (mcLoader is null)
        {
            _logger.LogError("Unsupported mod loader type: {Loader}", effectiveLoader);
            return LoaderInstallResult.Failed($"Unsupported mod loader type: {effectiveLoader}");
        }

        if (!_installers.TryGetValue(mcLoader.Value, out var installer))
        {
            _logger.LogError("No installer found for loader type: {Loader}", mcLoader);
            return LoaderInstallResult.Failed($"No installer available for {effectiveLoader}");
        }

        // Default to "latest" if no specific version provided
        var effectiveLoaderVersion = string.IsNullOrEmpty(loaderVersion) ? "latest" : loaderVersion;

        _logger.LogInformation(
            "Installing {Loader} {Version} for Minecraft {McVersion}",
            loader, effectiveLoaderVersion, minecraftVersion);

        // Create install context
        var context = new MinecraftInstallContext
        {
            Server = server,
            VersionString = $"{mcLoader.Value.ToString().ToLowerInvariant()}-{minecraftVersion}-{effectiveLoaderVersion}",
            LoaderType = mcLoader.Value,
            MinecraftVersion = minecraftVersion,
            LoaderVersion = effectiveLoaderVersion,
            JavaPath = GetJavaPath(server),
            RequiredJavaVersion = GetRequiredJavaVersion(minecraftVersion)
        };

        // Create progress adapter
        var progressAdapter = progress is not null
            ? new Progress<OfficialInstallProgress>(p =>
            {
                progress.Report(new ModpackInstallProgress
                {
                    Phase = ModpackInstallPhase.InstallingLoader,
                    Message = $"Installing {loader}: {p.Message}",
                    Percentage = 40 + (p.Percentage * 0.2), // 40-60% range for loader installation
                    CurrentFile = p.Message
                });
            })
            : null;

        // Run the installer
        var result = await installer.InstallAsync(context, progressAdapter, ct).ConfigureAwait(false);

        if (result.Success)
        {
            _logger.LogInformation(
                "Successfully installed {Loader} for Minecraft {McVersion}",
                loader, minecraftVersion);
        }
        else
        {
            _logger.LogError(
                "Failed to install {Loader}: {Error}",
                loader, result.Error);
        }

        return result;
    }

    /// <summary>
    /// Gets the startup command info for the installed loader.
    /// </summary>
    public StartupCommandInfo? GetStartupCommand(
        GameServer server,
        ModLoaderType loader,
        string minecraftVersion,
        string? loaderVersion)
    {
        if (loader == ModLoaderType.None)
            return null;

        var mcLoader = MapToMinecraftLoader(loader);
        if (mcLoader is null || !_installers.TryGetValue(mcLoader.Value, out var installer))
            return null;

        var effectiveLoaderVersion = string.IsNullOrEmpty(loaderVersion) ? "latest" : loaderVersion;

        var context = new MinecraftInstallContext
        {
            Server = server,
            VersionString = $"{mcLoader.Value.ToString().ToLowerInvariant()}-{minecraftVersion}-{effectiveLoaderVersion}",
            LoaderType = mcLoader.Value,
            MinecraftVersion = minecraftVersion,
            LoaderVersion = effectiveLoaderVersion,
            JavaPath = GetJavaPath(server),
            RequiredJavaVersion = GetRequiredJavaVersion(minecraftVersion)
        };

        return installer.GetStartupCommand(context);
    }

    /// <summary>
    /// Maps ModLoaderType (from modpack DTOs) to MinecraftLoader (from installer system).
    /// </summary>
    private static MinecraftLoader? MapToMinecraftLoader(ModLoaderType loader)
    {
        return loader switch
        {
            ModLoaderType.Forge => MinecraftLoader.Forge,
            ModLoaderType.NeoForge => MinecraftLoader.NeoForge,
            ModLoaderType.Fabric => MinecraftLoader.Fabric,
            ModLoaderType.Quilt => MinecraftLoader.Quilt,
            ModLoaderType.None => null,
            _ => null
        };
    }

    /// <summary>
    /// Gets the Java path configured for the server.
    /// </summary>
    private static string? GetJavaPath(GameServer server)
    {
        return server.NativeConfig?.JavaPath;
    }

    /// <summary>
    /// Determines the required Java version based on Minecraft version.
    /// </summary>
    private static int GetRequiredJavaVersion(string minecraftVersion)
    {
        var parts = minecraftVersion.Split('.');
        if (parts.Length < 2 || !int.TryParse(parts[1], out var minor))
            return 21; // Default to latest LTS

        // MC 1.20.5+ requires Java 21
        // MC 1.18-1.20.4 requires Java 17
        // MC 1.17 requires Java 16
        // MC < 1.17 requires Java 8
        if (minor >= 21) return 21;
        if (minor == 20)
        {
            if (parts.Length >= 3 && int.TryParse(parts[2], out var patch) && patch >= 5)
                return 21;
            return 17;
        }
        if (minor >= 18) return 17;
        if (minor == 17) return 16;
        return 8;
    }

    /// <summary>
    /// Determines if NeoForge is required instead of Forge for a Minecraft version.
    /// Forge stopped development after MC 1.20.1 and rebranded to NeoForge for 1.20.2+.
    /// Many modpack providers still report "Forge" for these versions.
    /// </summary>
    private static bool IsNeoForgeRequired(string minecraftVersion)
    {
        var parts = minecraftVersion.Split('.');
        if (parts.Length < 2)
            return false;

        if (!int.TryParse(parts[0], out var major) || !int.TryParse(parts[1], out var minor))
            return false;

        // MC 1.20.2+ requires NeoForge (Forge is not available)
        if (major > 1) return true;
        if (major == 1 && minor > 20) return true;
        if (major == 1 && minor == 20 && parts.Length > 2)
        {
            if (int.TryParse(parts[2], out var patch) && patch >= 2)
                return true;
        }

        return false;
    }
}
