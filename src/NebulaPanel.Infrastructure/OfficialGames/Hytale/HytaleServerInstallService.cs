using System.Collections.Concurrent;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.OfficialGames.Hytale;

public class HytaleServerInstallService : IHytaleServerInstallService
{
    private readonly IHytaleDownloaderService _downloaderService;
    private readonly IHytaleAuthService _authService;
    private readonly IGameRepository _gameRepository;
    private readonly IGameServerRepository _serverRepository;
    private readonly IHytaleInstallNotifier _installNotifier;
    private readonly IServerPathResolver _pathResolver;
    private readonly ILogger<HytaleServerInstallService> _logger;

    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _activeInstallations = new();

    private static readonly IReadOnlyList<HytalePatchlineInfo> PatchlineInfos =
    [
        new(
            "release",
            "Release Channel",
            "Stable release builds for production servers",
            IsRecommended: true
        ),
        new(
            "pre-release",
            "Pre-release Channel",
            "Preview builds with new features (may be unstable)"
        )
    ];

    public HytaleServerInstallService(
        IHytaleDownloaderService downloaderService,
        IHytaleAuthService authService,
        IGameRepository gameRepository,
        IGameServerRepository serverRepository,
        IHytaleInstallNotifier installNotifier,
        IServerPathResolver pathResolver,
        ILogger<HytaleServerInstallService> logger)
    {
        _downloaderService = downloaderService;
        _authService = authService;
        _gameRepository = gameRepository;
        _serverRepository = serverRepository;
        _installNotifier = installNotifier;
        _pathResolver = pathResolver;
        _logger = logger;
    }

    public IReadOnlyList<HytalePatchlineInfo> GetPatchlineInfos() => PatchlineInfos;

    public async Task<HytaleVersionInfo?> GetVersionAsync(
        string patchline,
        Guid userId,
        CancellationToken ct = default)
    {
        try
        {
            var credentials = await _authService.GetDecryptedCredentialsAsync(userId, ct).ConfigureAwait(false);
            if (credentials == null)
            {
                _logger.LogWarning("No Hytale credentials found for user {UserId}", userId);
                return null;
            }

            return await _downloaderService.GetServerVersionAsync(patchline, credentials, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Hytale version for patchline {Patchline}", patchline);
            return null;
        }
    }

    public async Task<IReadOnlyDictionary<string, HytaleVersionInfo>> GetAllVersionsAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var result = new Dictionary<string, HytaleVersionInfo>();

        foreach (var patchline in PatchlineInfos)
        {
            var version = await GetVersionAsync(patchline.Patchline, userId, ct).ConfigureAwait(false);
            if (version != null)
            {
                result[patchline.Patchline] = version;
            }
        }

        return result;
    }

    public Task<Result<IReadOnlyList<string>>> ValidateWizardDataAsync(
        HytaleServerWizardData data,
        CancellationToken ct = default)
    {
        var errors = new List<string>();

        // Patchline validation
        if (string.IsNullOrWhiteSpace(data.Patchline))
        {
            errors.Add("A patchline must be selected.");
        }
        else if (!PatchlineInfos.Any(p => p.Patchline == data.Patchline))
        {
            errors.Add($"Invalid patchline: {data.Patchline}");
        }

        // Server name validation
        if (string.IsNullOrWhiteSpace(data.ServerName))
        {
            errors.Add("Server name is required.");
        }
        else if (data.ServerName.Length < 2)
        {
            errors.Add("Server name must be at least 2 characters.");
        }
        else if (data.ServerName.Length > 100)
        {
            errors.Add("Server name must be 100 characters or less.");
        }

        // Install path validation
        if (string.IsNullOrWhiteSpace(data.InstallPath))
        {
            errors.Add("Install path is required.");
        }

        // Port validation
        if (data.Port < 1 || data.Port > 65535)
        {
            errors.Add("Port must be between 1 and 65535.");
        }

        // Memory validation
        if (data.MinMemoryMb < 512)
        {
            errors.Add("Minimum memory must be at least 512 MB.");
        }

        if (data.MaxMemoryMb < data.MinMemoryMb)
        {
            errors.Add("Maximum memory must be greater than or equal to minimum memory.");
        }

        if (data.MaxMemoryMb > 65536)
        {
            errors.Add("Maximum memory cannot exceed 64 GB.");
        }

        // Collect warnings (returned in success list, not treated as errors)
        var warnings = new List<string>();
        if (!string.IsNullOrWhiteSpace(data.CustomJvmArguments))
        {
            var customArgs = data.CustomJvmArguments.ToUpperInvariant();
            if (customArgs.Contains("-XMS") || customArgs.Contains("-XMX"))
            {
                warnings.Add("Custom JVM arguments contain memory settings (-Xms or -Xmx). Consider using the memory fields instead.");
            }
        }

        return Task.FromResult(errors.Count > 0
            ? Result.Failure<IReadOnlyList<string>>(string.Join(" ", errors))
            : Result.Success<IReadOnlyList<string>>(warnings));
    }

    public Task<bool> IsPortAvailableAsync(int port, CancellationToken ct = default)
    {
        try
        {
            var properties = IPGlobalProperties.GetIPGlobalProperties();
            var listeners = properties.GetActiveTcpListeners();
            var isInUse = listeners.Any(l => l.Port == port);
            return Task.FromResult(!isInUse);
        }
        catch
        {
            // If we can't check, assume it's available
            return Task.FromResult(true);
        }
    }

    public async Task<Result<Guid>> InstallServerAsync(
        HytaleServerWizardData data,
        Guid ownerId,
        Guid installationId,
        CancellationToken ct = default)
    {
        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _activeInstallations[installationId] = cts;

        try
        {
            _logger.LogInformation(
                "Starting Hytale server installation {InstallationId} for patchline {Patchline}",
                installationId, data.Patchline);

            await _installNotifier.NotifyProgressAsync(
                installationId,
                HytaleInstallProgressDto.Starting(installationId),
                cts.Token).ConfigureAwait(false);

            // Get the Hytale game from database
            var game = await _gameRepository.GetBySlugAsync("hytale", cts.Token).ConfigureAwait(false);
            if (game is null)
            {
                var error = "Hytale game not found in database. Please ensure Hytale is registered as an official game.";
                await _installNotifier.NotifyProgressAsync(
                    installationId,
                    HytaleInstallProgressDto.Failed(installationId, error),
                    cts.Token).ConfigureAwait(false);
                return Result.Failure<Guid>(error);
            }

            // Check if server name already exists for this owner
            if (await _serverRepository.NameExistsForOwnerAsync(data.ServerName, ownerId, cancellationToken: cts.Token).ConfigureAwait(false))
            {
                var error = $"You already have a server named '{data.ServerName}'.";
                await _installNotifier.NotifyProgressAsync(
                    installationId,
                    HytaleInstallProgressDto.Failed(installationId, error),
                    cts.Token).ConfigureAwait(false);
                return Result.Failure<Guid>(error);
            }

            // Check if port is in use in database
            if (await _serverRepository.IsPortInUseAsync(data.Port, "0.0.0.0", cancellationToken: cts.Token).ConfigureAwait(false))
            {
                var error = $"Port {data.Port} is already in use by another server.";
                await _installNotifier.NotifyProgressAsync(
                    installationId,
                    HytaleInstallProgressDto.Failed(installationId, error),
                    cts.Token).ConfigureAwait(false);
                return Result.Failure<Guid>(error);
            }

            // Verify credentials
            await _installNotifier.NotifyProgressAsync(
                installationId,
                HytaleInstallProgressDto.ValidatingCredentials(installationId),
                cts.Token).ConfigureAwait(false);

            var credentials = await _authService.GetDecryptedCredentialsAsync(ownerId, cts.Token).ConfigureAwait(false);
            if (credentials == null || !credentials.IsValid)
            {
                var error = "Hytale credentials are missing or expired. Please reconnect your Hytale account.";
                await _installNotifier.NotifyProgressAsync(
                    installationId,
                    HytaleInstallProgressDto.Failed(installationId, error),
                    cts.Token).ConfigureAwait(false);
                return Result.Failure<Guid>(error);
            }

            await _installNotifier.NotifyProgressAsync(
                installationId,
                HytaleInstallProgressDto.Authenticating(installationId),
                cts.Token).ConfigureAwait(false);

            // Small delay to show progress
            await Task.Delay(200, cts.Token).ConfigureAwait(false);

            // Create the server entity
            await _installNotifier.NotifyProgressAsync(
                installationId,
                HytaleInstallProgressDto.CreatingServerRecord(installationId),
                cts.Token).ConfigureAwait(false);
            var server = new GameServer
            {
                Id = Guid.NewGuid(),
                Name = data.ServerName,
                GameId = game.Id,
                OwnerId = ownerId,
                InstallPath = data.InstallPath,
                PrimaryPort = data.Port,
                BindAddress = "0.0.0.0",
                DeploymentType = data.DeploymentType,
                Status = ServerStatus.Installing
            };

            // Configure based on deployment type
            if (data.DeploymentType == ServerDeploymentType.Native)
            {
                var bindAddress = string.IsNullOrEmpty(data.BindAddress) ? "0.0.0.0" : data.BindAddress;
                server.NativeConfig = new NativeConfiguration
                {
                    // Working directory is the Server subfolder where the JAR lives
                    WorkingDirectory = Path.Combine(data.InstallPath, "Server"),
                    ExecutablePath = GetJavaExecutablePath(),
                    Arguments = GetJavaArguments(data, bindAddress, data.InstallPath),
                    EnvironmentVariables = new Dictionary<string, string>()
                };
            }
            else
            {
                // Hytale server runs as a JAR file with Java 25
                // JAR is at Server/HytaleServer.jar, assets at Assets.zip
                // Command: java -Xms{min}M -Xmx{max}M {aotCacheArgs} {customJvmArgs} -jar HytaleServer.jar --assets ../Assets.zip --bind IP:PORT
                // Note: We don't include --backup args because Nebula Panel has its own backup system with retention policies
                var bindAddress = string.IsNullOrEmpty(data.BindAddress) ? "0.0.0.0" : data.BindAddress;
                var memoryArgs = $"-Xms{data.MinMemoryMb}M -Xmx{data.MaxMemoryMb}M";

                // Note: AOT cache detection happens at runtime in DockerServerExecutor since the file
                // may be downloaded with updates after initial installation
                var customArgs = string.IsNullOrWhiteSpace(data.CustomJvmArguments) ? "" : $" {data.CustomJvmArguments.Trim()}";

                server.DockerConfig = new DockerConfiguration
                {
                    Image = data.DockerImage ?? "eclipse-temurin",
                    Tag = data.DockerTag ?? "25-jre",
                    Limits = new ResourceLimits { MaxMemoryMb = data.MaxMemoryMb },
                    EnvironmentVariables = new Dictionary<string, string>(),
                    Ports =
                    [
                        new PortMapping { HostPort = data.Port, ContainerPort = data.Port, Protocol = "udp" }
                    ],
                    Volumes =
                    [
                        new VolumeMount { HostPath = data.InstallPath, ContainerPath = "/server" }
                    ],
                    WorkingDirectory = "/server/Server",
                    Command = $"java {memoryArgs}{customArgs} -jar HytaleServer.jar --assets ../Assets.zip --bind {bindAddress}:{data.Port}",
                    Tty = false  // Disabled - stdin works better without TTY on Docker/Windows
                };
            }

            // Save to database
            await _serverRepository.AddAsync(server, cts.Token).ConfigureAwait(false);

            // Download server files
            await _installNotifier.NotifyProgressAsync(
                installationId,
                HytaleInstallProgressDto.FetchingVersion(installationId),
                cts.Token).ConfigureAwait(false);

            // Ensure install directory exists
            Directory.CreateDirectory(data.InstallPath);

            await _installNotifier.NotifyProgressAsync(
                installationId,
                HytaleInstallProgressDto.StartingDownload(installationId),
                cts.Token).ConfigureAwait(false);

            var downloadProgress = new Progress<HytaleDownloadProgress>(p =>
            {
                // Only process forward progress phases - ignore Starting/Authenticating/FetchingVersion
                // since we've already passed those stages and don't want the UI to jump backwards
                HytaleInstallProgressDto? progressDto = p.Phase switch
                {
                    HytaleDownloadPhase.Downloading => HytaleInstallProgressDto.Downloading(
                        installationId, p.Percentage, p.BytesDownloaded, p.TotalBytes),
                    HytaleDownloadPhase.Verifying => HytaleInstallProgressDto.Verifying(installationId),
                    HytaleDownloadPhase.Extracting => HytaleInstallProgressDto.Extracting(installationId),
                    HytaleDownloadPhase.Complete => HytaleInstallProgressDto.Configuring(installationId),
                    // Ignore Starting, Authenticating, FetchingVersion - we're already past those
                    _ => null
                };

                if (progressDto is not null)
                {
                    _installNotifier.NotifyProgressAsync(installationId, progressDto, CancellationToken.None)
                        .ConfigureAwait(false).GetAwaiter().GetResult();
                }
            });

            var downloadResult = await _downloaderService.DownloadServerAsync(
                data.InstallPath,
                data.Patchline,
                credentials,
                downloadProgress,
                cts.Token).ConfigureAwait(false);

            if (!downloadResult.Success)
            {
                // Delete the server entity on failure
                await _serverRepository.DeleteAsync(server.Id, CancellationToken.None).ConfigureAwait(false);

                await _installNotifier.NotifyProgressAsync(
                    installationId,
                    HytaleInstallProgressDto.Failed(installationId, downloadResult.Error ?? "Download failed."),
                    CancellationToken.None).ConfigureAwait(false);

                return Result.Failure<Guid>(downloadResult.Error ?? "Download failed.");
            }

            // Configuring phase
            await _installNotifier.NotifyProgressAsync(
                installationId,
                HytaleInstallProgressDto.Configuring(installationId),
                CancellationToken.None).ConfigureAwait(false);

            // Update server with installed version and HytaleInfo
            await _installNotifier.NotifyProgressAsync(
                installationId,
                HytaleInstallProgressDto.SavingToDatabase(installationId),
                CancellationToken.None).ConfigureAwait(false);

            server.InstalledVersion = downloadResult.Version ?? data.ServerVersion;
            var installedVersion = downloadResult.Version ?? data.ServerVersion ?? "";
            server.HytaleInfo = new HytaleServerInfo
            {
                InstalledVersion = installedVersion,
                Patchline = data.Patchline,
                InstalledAt = DateTime.UtcNow,
                AvailableVersion = installedVersion,
                LastUpdateCheckAt = DateTime.UtcNow
            };
            server.Status = ServerStatus.Stopped;
            await _serverRepository.UpdateAsync(server, CancellationToken.None).ConfigureAwait(false);

            // Finalizing
            await _installNotifier.NotifyProgressAsync(
                installationId,
                HytaleInstallProgressDto.Finalizing(installationId),
                CancellationToken.None).ConfigureAwait(false);

            await Task.Delay(300, CancellationToken.None).ConfigureAwait(false);

            // Send completed progress
            await _installNotifier.NotifyProgressAsync(
                installationId,
                HytaleInstallProgressDto.Completed(installationId),
                CancellationToken.None).ConfigureAwait(false);

            // Notify completion via the separate complete event
            await _installNotifier.NotifyCompleteAsync(installationId, server.Id, true, null, CancellationToken.None)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Hytale server installation {InstallationId} completed successfully. Server ID: {ServerId}",
                installationId, server.Id);

            return Result.Success(server.Id);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Installation {InstallationId} was cancelled", installationId);
            await _installNotifier.NotifyProgressAsync(
                installationId,
                HytaleInstallProgressDto.Failed(installationId, "Installation was cancelled."),
                CancellationToken.None).ConfigureAwait(false);
            return Result.Failure<Guid>("Installation was cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Installation {InstallationId} failed with exception", installationId);
            await _installNotifier.NotifyProgressAsync(
                installationId,
                HytaleInstallProgressDto.Failed(installationId, ex.Message),
                CancellationToken.None).ConfigureAwait(false);
            return Result.Failure<Guid>($"Installation failed: {ex.Message}");
        }
        finally
        {
            _activeInstallations.TryRemove(installationId, out _);
        }
    }

    public Task<Result> CancelInstallationAsync(Guid installationId, CancellationToken ct = default)
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
        var sanitized = SanitizeFileName(serverName);
        var basePath = _pathResolver.GetServerBasePath();

        return OperatingSystem.IsWindows()
            ? Path.Combine(basePath, "HytaleServers", sanitized)
            : Path.Combine(basePath, "hytale-servers", sanitized);
    }

    private static string GetJavaExecutablePath()
    {
        // Use java from PATH or JAVA_HOME
        // On Windows, try to find java.exe; on Unix, just use 'java'
        var javaHome = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrEmpty(javaHome))
        {
            var javaPath = OperatingSystem.IsWindows()
                ? Path.Combine(javaHome, "bin", "java.exe")
                : Path.Combine(javaHome, "bin", "java");

            if (File.Exists(javaPath))
            {
                return javaPath;
            }
        }

        // Fall back to java in PATH
        return OperatingSystem.IsWindows() ? "java.exe" : "java";
    }

    private static string GetJavaArguments(HytaleServerWizardData data, string bindAddress, string? installPath = null)
    {
        // Hytale server command format:
        // java -Xms{min}M -Xmx{max}M {aotCacheArgs} {customJvmArgs} -jar HytaleServer.jar --assets ../Assets.zip --bind IP:PORT
        // Note: Working directory is Server/, so assets are one level up
        // Note: We don't include --backup args because Nebula Panel has its own backup system with retention policies
        var memoryArgs = $"-Xms{data.MinMemoryMb}M -Xmx{data.MaxMemoryMb}M";

        // Check for AOT cache file (faster startup if available)
        var aotArgs = "";
        if (!string.IsNullOrEmpty(installPath))
        {
            var aotCachePath = Path.Combine(installPath, "Server", "HytaleServer.aot");
            if (File.Exists(aotCachePath))
            {
                aotArgs = " -XX:AOTCache=HytaleServer.aot";
            }
        }

        var customArgs = string.IsNullOrWhiteSpace(data.CustomJvmArguments) ? "" : $" {data.CustomJvmArguments.Trim()}";

        return $"{memoryArgs}{aotArgs}{customArgs} -jar HytaleServer.jar --assets ../Assets.zip --bind {bindAddress}:{data.Port}";
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(name.Where(c => !invalid.Contains(c)).ToArray());
        return sanitized.Replace(' ', '-').ToLowerInvariant();
    }
}

/// <summary>
/// Interface for notifying clients about Hytale installation progress.
/// </summary>
public interface IHytaleInstallNotifier
{
    Task NotifyProgressAsync(Guid installationId, HytaleInstallProgressDto progress, CancellationToken ct = default);
    Task NotifyCompleteAsync(Guid installationId, Guid serverId, bool success, string? error, CancellationToken ct = default);
    Task NotifyLogLineAsync(Guid installationId, string line, CancellationToken ct = default);
}
