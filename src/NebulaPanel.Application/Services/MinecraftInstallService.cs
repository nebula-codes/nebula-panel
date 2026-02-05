using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Application.Services;

public class MinecraftInstallService : IMinecraftInstallService
{
    private readonly IOfficialGameRegistry _gameRegistry;
    private readonly IGameRepository _gameRepository;
    private readonly IGameServerRepository _serverRepository;
    private readonly IMinecraftInstallNotifier _installNotifier;
    private readonly IUnifiedModpackService _modpackService;
    private readonly IMinecraftConfigWriter _configWriter;
    private readonly ILogger<MinecraftInstallService> _logger;

    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _activeInstallations = new();

    private static readonly IReadOnlyList<MinecraftLoaderInfo> LoaderInfos =
    [
        new(
            MinecraftLoader.Paper,
            "Paper",
            "High performance fork of Spigot with async chunk loading and extensive API",
            "scroll-text",
            ["Plugins", "High Performance", "Async Chunks"],
            IsRecommended: true
        ),
        new(
            MinecraftLoader.Vanilla,
            "Vanilla",
            "Official Mojang server without any modifications",
            "box",
            ["No Mods", "Pure Experience", "Official"]
        ),
        new(
            MinecraftLoader.Spigot,
            "Spigot",
            "CraftBukkit fork with plugin support and performance improvements",
            "plug",
            ["Plugins", "Bukkit API", "Stable"]
        ),
        new(
            MinecraftLoader.Fabric,
            "Fabric",
            "Lightweight modding toolchain for performance mods",
            "layers",
            ["Mods", "Lightweight", "Performance"]
        ),
        new(
            MinecraftLoader.Forge,
            "Forge",
            "Traditional modding platform with extensive mod compatibility",
            "anvil",
            ["Mods", "Large Modpacks", "Mature Ecosystem"]
        ),
        new(
            MinecraftLoader.Purpur,
            "Purpur",
            "Paper fork with extra gameplay features and customization",
            "sparkles",
            ["Plugins", "Extra Features", "Configurable"]
        ),
        new(
            MinecraftLoader.Quilt,
            "Quilt",
            "Fabric fork with improved modding toolchain",
            "layout-grid",
            ["Mods", "Fabric Compatible", "Modern"]
        ),
        new(
            MinecraftLoader.Velocity,
            "Velocity",
            "Modern proxy server for connecting multiple Minecraft servers",
            "network",
            ["Proxy", "Modern", "High Performance"],
            IsProxy: true
        ),
        new(
            MinecraftLoader.BungeeCord,
            "BungeeCord",
            "Legacy proxy server for multi-server networks",
            "link",
            ["Proxy", "Legacy", "Compatible"],
            IsProxy: true
        )
    ];

    public MinecraftInstallService(
        IOfficialGameRegistry gameRegistry,
        IGameRepository gameRepository,
        IGameServerRepository serverRepository,
        IMinecraftInstallNotifier installNotifier,
        IUnifiedModpackService modpackService,
        IMinecraftConfigWriter configWriter,
        ILogger<MinecraftInstallService> logger)
    {
        _gameRegistry = gameRegistry;
        _gameRepository = gameRepository;
        _serverRepository = serverRepository;
        _installNotifier = installNotifier;
        _modpackService = modpackService;
        _configWriter = configWriter;
        _logger = logger;
    }

    public IReadOnlyList<MinecraftLoaderInfo> GetLoaderInfos() => LoaderInfos;

    public async Task<IReadOnlyList<GameVersionDto>> GetAllVersionsAsync(
        bool includeSnapshots = false,
        CancellationToken cancellationToken = default)
    {
        var provider = _gameRegistry.GetProvider("minecraft-java");
        if (provider is null)
        {
            _logger.LogError("Minecraft provider not found in registry");
            return [];
        }

        var versions = await provider.GetAvailableVersionsAsync(cancellationToken).ConfigureAwait(false);

        return versions
            .Where(v => includeSnapshots || v.VersionType == GameVersionType.Release)
            .Select(v => new GameVersionDto(
                v.Version,
                v.DisplayName,
                v.ReleaseDate,
                v.VersionType,
                v.LoaderType,
                v.MinecraftVersion,
                v.IsRecommended))
            .ToList();
    }

    public async Task<IReadOnlyList<GameVersionDto>> GetVersionsForLoaderAsync(
        MinecraftLoader loader,
        bool includeSnapshots = false,
        CancellationToken cancellationToken = default)
    {
        var allVersions = await GetAllVersionsAsync(includeSnapshots, cancellationToken).ConfigureAwait(false);

        return allVersions
            .Where(v => v.LoaderType == loader)
            .ToList();
    }

    public async Task<IReadOnlyList<string>> GetMinecraftVersionsAsync(
        MinecraftLoader loader,
        bool includeSnapshots = false,
        CancellationToken cancellationToken = default)
    {
        var versions = await GetVersionsForLoaderAsync(loader, includeSnapshots, cancellationToken)
            .ConfigureAwait(false);

        return versions
            .Select(v => v.MinecraftVersion ?? v.Version)
            .Distinct()
            .OrderByDescending(v => ParseVersion(v))
            .ToList();
    }

    public async Task<IReadOnlyList<GameVersionDto>> GetLoaderBuildsAsync(
        MinecraftLoader loader,
        string minecraftVersion,
        CancellationToken cancellationToken = default)
    {
        var versions = await GetVersionsForLoaderAsync(loader, includeSnapshots: true, cancellationToken)
            .ConfigureAwait(false);

        return versions
            .Where(v => v.MinecraftVersion == minecraftVersion || v.Version.StartsWith(minecraftVersion))
            .OrderByDescending(v => v.IsRecommended)
            .ThenByDescending(v => v.ReleaseDate)
            .ToList();
    }

    public Task<Result<IReadOnlyList<string>>> ValidateWizardDataAsync(
        MinecraftServerWizardData data,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        // Modpack flow validation
        if (data.SetupType == MinecraftSetupType.Modpack)
        {
            if (data.SelectedModpack is null)
            {
                errors.Add("A modpack must be selected.");
            }
        }
        else
        {
            // Loader flow validation
            // Step 1 validation: Loader
            if (!Enum.IsDefined(data.SelectedLoader))
            {
                errors.Add("Invalid loader selection.");
            }

            // Step 2 validation: Version
            if (string.IsNullOrWhiteSpace(data.MinecraftVersion))
            {
                errors.Add("Minecraft version is required.");
            }
        }

        // Step 3 validation: Server configuration
        if (string.IsNullOrWhiteSpace(data.ServerName))
        {
            errors.Add("Server name is required.");
        }
        else if (data.ServerName.Length > 100)
        {
            errors.Add("Server name must be 100 characters or less.");
        }

        if (string.IsNullOrWhiteSpace(data.InstallPath))
        {
            errors.Add("Install path is required.");
        }

        if (data.Port < 1 || data.Port > 65535)
        {
            errors.Add("Port must be between 1 and 65535.");
        }

        if (data.MinMemoryMb < 512)
        {
            errors.Add("Minimum memory must be at least 512 MB.");
        }

        if (data.MaxMemoryMb < data.MinMemoryMb)
        {
            errors.Add("Maximum memory must be greater than or equal to minimum memory.");
        }

        if (data.MaxMemoryMb > 32768) // 32GB
        {
            errors.Add("Maximum memory cannot exceed 32 GB.");
        }

        if (data.EnableRcon)
        {
            if (data.RconPort < 1 || data.RconPort > 65535)
            {
                errors.Add("RCON port must be between 1 and 65535.");
            }

            if (data.RconPort == data.Port)
            {
                errors.Add("RCON port must be different from game port.");
            }
        }

        // Step 4 validation: World settings
        if (string.IsNullOrWhiteSpace(data.WorldName))
        {
            errors.Add("World name is required.");
        }

        if (data.MaxPlayers < 1 || data.MaxPlayers > 1000)
        {
            errors.Add("Max players must be between 1 and 1000.");
        }

        if (errors.Count > 0)
        {
            return Task.FromResult(Result.Success<IReadOnlyList<string>>(errors));
        }

        return Task.FromResult(Result.Success<IReadOnlyList<string>>([]));
    }

    public Task<bool> IsPortAvailableAsync(int port, CancellationToken cancellationToken = default)
    {
        try
        {
            var ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            var tcpConnections = ipGlobalProperties.GetActiveTcpListeners();

            var isInUse = tcpConnections.Any(endpoint => endpoint.Port == port);
            return Task.FromResult(!isInUse);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check port availability for port {Port}", port);
            return Task.FromResult(true); // Assume available if check fails
        }
    }

    public async Task<Result<Guid>> InstallServerAsync(
        MinecraftServerWizardData data,
        Guid ownerId,
        Guid installationId,
        CancellationToken cancellationToken = default)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _activeInstallations[installationId] = cts;

        try
        {
            _logger.LogInformation("Starting Minecraft server installation {InstallationId} for {Loader} {Version}",
                installationId, data.SelectedLoader, data.MinecraftVersion);

            await _installNotifier.NotifyProgressAsync(
                installationId,
                MinecraftInstallProgressDto.Starting(installationId),
                cts.Token).ConfigureAwait(false);

            // Get the Minecraft game from database
            var game = await _gameRepository.GetBySlugAsync("minecraft-java", cts.Token).ConfigureAwait(false);
            if (game is null)
            {
                var error = "Minecraft game not found in database. Please run the official game seeder.";
                await _installNotifier.NotifyProgressAsync(
                    installationId,
                    MinecraftInstallProgressDto.Failed(installationId, error),
                    cts.Token).ConfigureAwait(false);
                return Result.Failure<Guid>(error);
            }

            // Check if server name already exists for this owner
            if (await _serverRepository.NameExistsForOwnerAsync(data.ServerName, ownerId, cancellationToken: cts.Token).ConfigureAwait(false))
            {
                var error = $"You already have a server named '{data.ServerName}'.";
                await _installNotifier.NotifyProgressAsync(
                    installationId,
                    MinecraftInstallProgressDto.Failed(installationId, error),
                    cts.Token).ConfigureAwait(false);
                return Result.Failure<Guid>(error);
            }

            // Check if port is in use in database
            if (await _serverRepository.IsPortInUseAsync(data.Port, "0.0.0.0", cancellationToken: cts.Token).ConfigureAwait(false))
            {
                var error = $"Port {data.Port} is already in use by another server.";
                await _installNotifier.NotifyProgressAsync(
                    installationId,
                    MinecraftInstallProgressDto.Failed(installationId, error),
                    cts.Token).ConfigureAwait(false);
                return Result.Failure<Guid>(error);
            }

            // Build the version string for the installer
            var version = BuildVersionString(data);

            // Create the server entity
            var server = CreateServerEntity(data, game.Id, ownerId);

            // Save server to database first (AddAsync saves internally)
            await _serverRepository.AddAsync(server, cts.Token).ConfigureAwait(false);

            _logger.LogDebug("Created server entity {ServerId} for installation", server.Id);

            // Handle modpack vs loader installation
            if (data.SetupType == MinecraftSetupType.Modpack && data.SelectedModpack is not null)
            {
                // Modpack installation flow
                _logger.LogInformation("Installing modpack {ModpackName} for server {ServerId}",
                    data.SelectedModpack.ModpackName, server.Id);

                var modpackProgress = new Progress<ModpackInstallProgress>(async p =>
                {
                    var dto = new MinecraftInstallProgressDto
                    {
                        InstallationId = installationId,
                        Phase = p.Phase.ToString(),
                        Message = p.Message ?? "",
                        Percentage = p.Percentage,
                        BytesDownloaded = p.BytesDownloaded,
                        TotalBytes = p.TotalBytes,
                        Timestamp = DateTime.UtcNow
                    };

                    await _installNotifier.NotifyProgressAsync(installationId, dto, CancellationToken.None)
                        .ConfigureAwait(false);
                });

                var modpackResult = await _modpackService.InstallModpackAsync(
                    server,
                    data.SelectedModpack.Provider,
                    data.SelectedModpack.ModpackId,
                    data.SelectedModpack.VersionId,
                    modpackProgress,
                    cts.Token).ConfigureAwait(false);

                if (!modpackResult.Success)
                {
                    // Delete the server entity on failure
                    await _serverRepository.DeleteAsync(server.Id, CancellationToken.None).ConfigureAwait(false);

                    await _installNotifier.NotifyProgressAsync(
                        installationId,
                        MinecraftInstallProgressDto.Failed(installationId, modpackResult.Error ?? "Modpack installation failed."),
                        CancellationToken.None).ConfigureAwait(false);

                    return Result.Failure<Guid>(modpackResult.Error ?? "Modpack installation failed.");
                }

                // Update server with installed version
                server.InstalledVersion = $"{modpackResult.ModpackName} {modpackResult.ModpackVersion}";
                await _serverRepository.UpdateAsync(server, CancellationToken.None).ConfigureAwait(false);

                // Write server.properties with wizard settings (overwrite modpack defaults)
                await WriteServerPropertiesAsync(server.InstallPath, data, cts.Token).ConfigureAwait(false);
            }
            else
            {
                // Standard loader installation flow
                // Get the provider and install
                var provider = _gameRegistry.GetProvider("minecraft-java");
                if (provider is null)
                {
                    var error = "Minecraft provider not found.";
                    await _installNotifier.NotifyProgressAsync(
                        installationId,
                        MinecraftInstallProgressDto.Failed(installationId, error),
                        cts.Token).ConfigureAwait(false);
                    return Result.Failure<Guid>(error);
                }

                // Create progress adapter
                var progress = new Progress<OfficialInstallProgress>(async p =>
                {
                    var dto = new MinecraftInstallProgressDto
                    {
                        InstallationId = installationId,
                        Phase = p.Phase,
                        Message = p.Message,
                        Percentage = p.Percentage,
                        BytesDownloaded = p.BytesDownloaded,
                        TotalBytes = p.TotalBytes,
                        Timestamp = p.Timestamp
                    };

                    await _installNotifier.NotifyProgressAsync(installationId, dto, CancellationToken.None)
                        .ConfigureAwait(false);
                });

                var result = await provider.InstallServerAsync(server, version, progress, cts.Token)
                    .ConfigureAwait(false);

                if (!result.Success)
                {
                    // Delete the server entity on failure
                    await _serverRepository.DeleteAsync(server.Id, CancellationToken.None).ConfigureAwait(false);

                    await _installNotifier.NotifyProgressAsync(
                        installationId,
                        MinecraftInstallProgressDto.Failed(installationId, result.Error ?? "Installation failed."),
                        CancellationToken.None).ConfigureAwait(false);

                    return Result.Failure<Guid>(result.Error ?? "Installation failed.");
                }

                // Update server with installed version
                server.InstalledVersion = result.InstalledVersion;
                await _serverRepository.UpdateAsync(server, CancellationToken.None).ConfigureAwait(false);

                // Write server.properties with wizard settings
                await WriteServerPropertiesAsync(server.InstallPath, data, cts.Token).ConfigureAwait(false);
            }

            // Notify completion
            await _installNotifier.NotifyCompleteAsync(installationId, server.Id, true, null, CancellationToken.None)
                .ConfigureAwait(false);

            _logger.LogInformation("Minecraft server installation {InstallationId} completed successfully. Server ID: {ServerId}",
                installationId, server.Id);

            return Result.Success(server.Id);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Installation {InstallationId} was cancelled", installationId);
            await _installNotifier.NotifyProgressAsync(
                installationId,
                MinecraftInstallProgressDto.Failed(installationId, "Installation was cancelled."),
                CancellationToken.None).ConfigureAwait(false);
            return Result.Failure<Guid>("Installation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Installation {InstallationId} failed with exception", installationId);
            await _installNotifier.NotifyProgressAsync(
                installationId,
                MinecraftInstallProgressDto.Failed(installationId, ex.Message),
                CancellationToken.None).ConfigureAwait(false);
            return Result.Failure<Guid>($"Installation failed: {ex.Message}");
        }
        finally
        {
            _activeInstallations.TryRemove(installationId, out _);
            cts.Dispose();
        }
    }

    public Task<Result> CancelInstallationAsync(
        Guid installationId,
        CancellationToken cancellationToken = default)
    {
        if (_activeInstallations.TryGetValue(installationId, out var cts))
        {
            cts.Cancel();
            _logger.LogInformation("Cancellation requested for installation {InstallationId}", installationId);
            return Task.FromResult(Result.Success());
        }

        return Task.FromResult(Result.Failure("Installation not found or already completed."));
    }

    public string GetDefaultInstallPath(string serverName)
    {
        var sanitizedName = SanitizeFileName(serverName);
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        return OperatingSystem.IsWindows()
            ? Path.Combine(basePath, "MinecraftServers", sanitizedName)
            : Path.Combine(basePath, "minecraft-servers", sanitizedName);
    }

    public string GenerateRconPassword()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var data = RandomNumberGenerator.GetBytes(16);
        var result = new char[16];

        for (int i = 0; i < result.Length; i++)
        {
            result[i] = chars[data[i] % chars.Length];
        }

        return new string(result);
    }

    private static string BuildVersionString(MinecraftServerWizardData data)
    {
        // Build version string that the installer expects
        // Format depends on loader type
        if (!string.IsNullOrEmpty(data.LoaderVersion))
        {
            return data.LoaderVersion; // Full version string like "paper-1.20.4-496"
        }

        // Build from components
        return data.SelectedLoader switch
        {
            MinecraftLoader.Vanilla => data.MinecraftVersion!,
            MinecraftLoader.Paper => $"paper-{data.MinecraftVersion}-latest",
            MinecraftLoader.Purpur => $"purpur-{data.MinecraftVersion}-latest",
            MinecraftLoader.Spigot => $"spigot-{data.MinecraftVersion}",
            MinecraftLoader.Fabric => $"fabric-{data.MinecraftVersion}-latest",
            MinecraftLoader.Forge => $"forge-{data.MinecraftVersion}-recommended",
            MinecraftLoader.Quilt => $"quilt-{data.MinecraftVersion}-latest",
            MinecraftLoader.Velocity => $"velocity-latest",
            MinecraftLoader.BungeeCord => $"bungeecord-latest",
            _ => data.MinecraftVersion!
        };
    }

    private GameServer CreateServerEntity(MinecraftServerWizardData data, Guid gameId, Guid ownerId)
    {
        var rconPassword = data.EnableRcon
            ? data.RconPassword ?? GenerateRconPassword()
            : null;

        var additionalPorts = new Dictionary<string, int>();
        if (data.EnableRcon)
        {
            additionalPorts["rcon"] = data.RconPort;
        }

        var server = new GameServer
        {
            Id = Guid.NewGuid(),
            Name = data.ServerName,
            GameId = gameId,
            OwnerId = ownerId,
            DeploymentType = data.DeploymentType,
            InstallPath = data.InstallPath,
            PrimaryPort = data.Port,
            AdditionalPorts = additionalPorts,
            BindAddress = "0.0.0.0",
            Status = ServerStatus.Stopped,
            ResourceLimits = new ResourceLimits
            {
                MaxMemoryMb = data.MaxMemoryMb
            }
        };

        // Set deployment-specific configuration
        if (data.DeploymentType == ServerDeploymentType.Native)
        {
            server.NativeConfig = new NativeConfiguration
            {
                WorkingDirectory = data.InstallPath,
                ExecutablePath = "server.jar",
                JavaArguments = $"-Xms{data.MinMemoryMb}M -Xmx{data.MaxMemoryMb}M"
            };
        }
        else
        {
            // For Docker, the internal ports stay at defaults (25565 game, 25575 RCON)
            // Port mappings handle exposing on the user's desired host ports
            var portMappings = new List<PortMapping>
            {
                new() { HostPort = data.Port, ContainerPort = 25565, Protocol = "tcp" }
            };

            if (data.EnableRcon)
            {
                portMappings.Add(new() { HostPort = data.RconPort, ContainerPort = 25575, Protocol = "tcp" });
            }

            server.DockerConfig = new DockerConfiguration
            {
                Image = data.DockerImage ?? "itzg/minecraft-server",
                Tag = data.DockerTag ?? "latest",
                EnvironmentVariables = new Dictionary<string, string>
                {
                    ["EULA"] = "TRUE",
                    ["TYPE"] = data.SelectedLoader.ToString().ToUpperInvariant(),
                    ["VERSION"] = data.MinecraftVersion ?? "LATEST",
                    ["MEMORY"] = $"{data.MaxMemoryMb}M"
                },
                Ports = portMappings,
                RestartPolicy = RestartPolicy.UnlessStopped
            };
        }

        // Set RCON configuration
        if (data.EnableRcon)
        {
            server.RconConfig = new RconConfiguration
            {
                Enabled = true,
                Protocol = RconProtocolType.Minecraft,
                Host = "127.0.0.1",
                Port = data.RconPort,
                Password = rconPassword
            };
        }

        return server;
    }

    private static string SanitizeFileName(string name)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalidChars.Contains(c)).ToArray());
        return sanitized.Replace(' ', '-').ToLowerInvariant();
    }

    private static Version ParseVersion(string versionString)
    {
        var cleanVersion = versionString.Split('_')[0].Split('-')[0];
        var parts = cleanVersion.Split('.');

        try
        {
            var major = parts.Length > 0 ? int.Parse(parts[0]) : 0;
            var minor = parts.Length > 1 ? int.Parse(parts[1]) : 0;
            var patch = parts.Length > 2 ? int.Parse(parts[2]) : 0;
            return new Version(major, minor, patch);
        }
        catch
        {
            return new Version(0, 0, 0);
        }
    }

    private async Task WriteServerPropertiesAsync(
        string installPath,
        MinecraftServerWizardData data,
        CancellationToken cancellationToken)
    {
        var settings = MinecraftServerPropertiesSettings.FromWizard(data);
        await _configWriter.WriteServerPropertiesAsync(
            installPath, settings, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Interface for notifying installation progress via SignalR.
/// </summary>
public interface IMinecraftInstallNotifier
{
    Task NotifyProgressAsync(Guid installationId, MinecraftInstallProgressDto progress, CancellationToken cancellationToken = default);
    Task NotifyCompleteAsync(Guid installationId, Guid serverId, bool success, string? error, CancellationToken cancellationToken = default);
    Task NotifyLogLineAsync(Guid installationId, string line, CancellationToken cancellationToken = default);
}
