using System.Text.Json;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.OfficialGames;

/// <summary>
/// Generic provider for games defined via JSON files.
/// Handles version fetching and installation for simple games.
/// </summary>
public class JsonGameProvider : IJsonGameDefinitionProvider
{
    private readonly JsonGameDefinition _definition;
    private readonly IConfigurationSchemaLoader _schemaLoader;
    private readonly ISteamCmdService? _steamCmd;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServerPathResolver _pathResolver;
    private readonly ILogger<JsonGameProvider> _logger;
    private Dictionary<string, ConfigurationSchema>? _loadedSchemas;

    public string GameSlug => _definition.Slug;
    public string DefinitionFilePath { get; }
    public bool IsUserDefined { get; }

    public JsonGameProvider(
        JsonGameDefinition definition,
        string definitionPath,
        bool isUserDefined,
        IConfigurationSchemaLoader schemaLoader,
        ISteamCmdService? steamCmd,
        IHttpClientFactory httpClientFactory,
        IServerPathResolver pathResolver,
        ILogger<JsonGameProvider> logger)
    {
        _definition = definition;
        DefinitionFilePath = definitionPath;
        IsUserDefined = isUserDefined;
        _schemaLoader = schemaLoader;
        _steamCmd = steamCmd;
        _httpClientFactory = httpClientFactory;
        _pathResolver = pathResolver;
        _logger = logger;
    }

    public string GetDefaultInstallPath(string serverName)
    {
        var sanitized = SanitizeForPath(serverName);
        var basePath = _pathResolver.GetServerBasePath();
        var folderName = OperatingSystem.IsWindows()
            ? $"{_definition.Name.Replace(" ", "")}Servers"
            : $"{_definition.Slug}-servers";

        return Path.Combine(basePath, folderName, sanitized);
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

    public GameDefinition GetGameDefinition()
    {
        // Lazy load schemas
        _loadedSchemas ??= LoadSchemasSync();
        return _definition.ToGameDefinition(_loadedSchemas);
    }

    /// <summary>
    /// Loads schemas synchronously. GetGameDefinition() is defined as sync in IOfficialGameProvider
    /// and is called from many sync contexts, so we cannot make it async without a large refactor.
    /// Task.Run avoids potential deadlocks by ensuring the async work runs on the thread pool
    /// rather than blocking on the caller's synchronization context.
    /// </summary>
    private Dictionary<string, ConfigurationSchema> LoadSchemasSync()
    {
        var schemas = new Dictionary<string, ConfigurationSchema>();

        foreach (var (configFile, schemaFile) in _definition.ConfigSchemaFiles)
        {
            var schema = Task.Run(() => _schemaLoader.LoadSchemaAsync(_definition.Slug, schemaFile))
                .GetAwaiter().GetResult();

            if (schema is not null)
            {
                schemas[configFile] = schema;
            }
        }

        return schemas;
    }

    public async Task<IReadOnlyList<GameVersionInfo>> GetAvailableVersionsAsync(
        CancellationToken cancellationToken = default)
    {
        var versions = new List<GameVersionInfo>();

        foreach (var source in _definition.VersionSources)
        {
            try
            {
                var fetched = await FetchVersionsFromSourceAsync(source, cancellationToken).ConfigureAwait(false);
                versions.AddRange(fetched);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch versions from source {Type} for {GameSlug}",
                    source.Type, _definition.Slug);
            }
        }

        return versions
            .DistinctBy(v => v.Version)
            .OrderByDescending(v => v.ReleaseDate)
            .ToList();
    }

    private async Task<List<GameVersionInfo>> FetchVersionsFromSourceAsync(
        VersionSourceConfig source,
        CancellationToken cancellationToken)
    {
        return source.Type.ToLowerInvariant() switch
        {
            "steamcmd" => await FetchSteamVersionsAsync(source, cancellationToken).ConfigureAwait(false),
            "httpjson" => await FetchHttpJsonVersionsAsync(source, cancellationToken).ConfigureAwait(false),
            "static" => FetchStaticVersions(source),
            _ => []
        };
    }

    private async Task<List<GameVersionInfo>> FetchSteamVersionsAsync(
        VersionSourceConfig source,
        CancellationToken cancellationToken)
    {
        // For Steam-based games, we typically don't have multiple versions to list
        // Return a single "latest" version since SteamCMD handles versioning
        var steamAppId = _definition.SteamAppId ?? source.Url;

        if (string.IsNullOrEmpty(steamAppId))
        {
            return [];
        }

        return
        [
            new GameVersionInfo
            {
                Version = "latest",
                DisplayName = "Latest Steam Build",
                ReleaseDate = DateTime.UtcNow,
                VersionType = Domain.Enums.GameVersionType.Release,
                IsRecommended = true
            }
        ];
    }

    private async Task<List<GameVersionInfo>> FetchHttpJsonVersionsAsync(
        VersionSourceConfig source,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(source.Url))
        {
            return [];
        }

        var client = _httpClientFactory.CreateClient("JsonGameProvider");
        var response = await client.GetAsync(source.Url, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);

        var versions = new List<GameVersionInfo>();
        var element = doc.RootElement;

        // Navigate to the versions array using JSONPath-like syntax
        if (!string.IsNullOrEmpty(source.JsonPath))
        {
            foreach (var segment in source.JsonPath.Split('.'))
            {
                if (element.TryGetProperty(segment, out var next))
                {
                    element = next;
                }
                else
                {
                    return [];
                }
            }
        }

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var version = ExtractVersionFromElement(item, source);
                if (version is not null)
                {
                    versions.Add(version);
                }
            }
        }

        return versions;
    }

    private GameVersionInfo? ExtractVersionFromElement(JsonElement element, VersionSourceConfig source)
    {
        var versionField = source.VersionField ?? "version";
        var dateField = source.DateField ?? "releaseDate";

        if (!element.TryGetProperty(versionField, out var versionElement))
        {
            return null;
        }

        var version = versionElement.GetString();
        if (string.IsNullOrEmpty(version))
        {
            return null;
        }

        DateTime? releaseDate = null;
        if (element.TryGetProperty(dateField, out var dateElement))
        {
            if (dateElement.TryGetDateTime(out var date))
            {
                releaseDate = date;
            }
        }

        return new GameVersionInfo
        {
            Version = version,
            DisplayName = version,
            ReleaseDate = releaseDate ?? DateTime.UtcNow,
            VersionType = Domain.Enums.GameVersionType.Release,
            IsRecommended = false
        };
    }

    private static List<GameVersionInfo> FetchStaticVersions(VersionSourceConfig source)
    {
        if (source.StaticVersions is null || source.StaticVersions.Count == 0)
        {
            return [];
        }

        return source.StaticVersions
            .Select((v, i) => new GameVersionInfo
            {
                Version = v,
                DisplayName = v,
                ReleaseDate = DateTime.UtcNow,
                VersionType = Domain.Enums.GameVersionType.Release,
                IsRecommended = i == 0
            })
            .ToList();
    }

    public async Task<ServerInstallationResult> InstallServerAsync(
        GameServer server,
        string version,
        IProgress<OfficialInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var installConfig = _definition.InstallConfig;
        if (installConfig is null)
        {
            return ServerInstallationResult.Failed("No installation configuration defined for this game");
        }

        progress?.Report(OfficialInstallProgress.Starting($"Installing {_definition.Name}..."));

        try
        {
            return installConfig.Type.ToLowerInvariant() switch
            {
                "steamcmd" => await InstallViaSteamCmdAsync(server, version, progress, cancellationToken)
                    .ConfigureAwait(false),
                "httpdownload" => await InstallViaHttpDownloadAsync(server, version, installConfig, progress, cancellationToken)
                    .ConfigureAwait(false),
                "manual" => ServerInstallationResult.Succeeded(server.InstallPath, "manual"),
                _ => ServerInstallationResult.Failed($"Unknown installation type: {installConfig.Type}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install {GameSlug} server", _definition.Slug);
            return ServerInstallationResult.Failed(ex.Message);
        }
    }

    private async Task<ServerInstallationResult> InstallViaSteamCmdAsync(
        GameServer server,
        string version,
        IProgress<OfficialInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (_steamCmd is null)
        {
            return ServerInstallationResult.Failed("SteamCMD service is not available");
        }

        var steamAppId = _definition.InstallConfig?.SteamAppId ?? _definition.SteamAppId;
        if (string.IsNullOrEmpty(steamAppId))
        {
            return ServerInstallationResult.Failed("No Steam App ID configured for this game");
        }

        progress?.Report(OfficialInstallProgress.Progress("SteamCMD", "Downloading via SteamCMD...", 10));

        var success = await _steamCmd.InstallOrUpdateGameAsync(
            steamAppId,
            server.InstallPath,
            branch: null,
            betaPassword: null,
            validate: _definition.InstallConfig?.ValidateAfterInstall ?? true,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (!success)
        {
            return ServerInstallationResult.Failed("SteamCMD installation failed");
        }

        // Run post-install commands if any
        if (_definition.InstallConfig?.PostInstallCommands?.Count > 0)
        {
            progress?.Report(OfficialInstallProgress.Progress("Post-Install", "Running post-install commands...", 90));
            await RunPostInstallCommandsAsync(server.InstallPath, cancellationToken).ConfigureAwait(false);
        }

        progress?.Report(OfficialInstallProgress.Completed());
        return ServerInstallationResult.Succeeded(server.InstallPath, version);
    }

    private async Task<ServerInstallationResult> InstallViaHttpDownloadAsync(
        GameServer server,
        string version,
        JsonInstallConfig installConfig,
        IProgress<OfficialInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(installConfig.DownloadUrlTemplate))
        {
            return ServerInstallationResult.Failed("No download URL template configured");
        }

        var url = installConfig.DownloadUrlTemplate
            .Replace("{version}", version)
            .Replace("{platform}", Environment.OSVersion.Platform == PlatformID.Win32NT ? "windows" : "linux");

        progress?.Report(OfficialInstallProgress.Progress("Download", $"Downloading from {url}...", 10));

        var client = _httpClientFactory.CreateClient("JsonGameProvider");
        var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        // Create install directory
        Directory.CreateDirectory(server.InstallPath);

        // Download to temp file
        var tempFile = Path.GetTempFileName();
        try
        {
            var totalBytes = response.Content.Headers.ContentLength;
            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            await using var fileStream = File.Create(tempFile);

            var buffer = new byte[81920];
            long downloaded = 0;
            int read;

            while ((read = await contentStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                downloaded += read;

                if (totalBytes.HasValue)
                {
                    progress?.Report(OfficialInstallProgress.Downloading(downloaded, totalBytes.Value));
                }
            }

            fileStream.Close();

            // Extract if it's an archive
            if (url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                progress?.Report(OfficialInstallProgress.Progress("Extracting", "Extracting files...", 90));
                System.IO.Compression.ZipFile.ExtractToDirectory(tempFile, server.InstallPath, overwriteFiles: true);
            }
            else
            {
                // Copy single file
                var fileName = Path.GetFileName(new Uri(url).LocalPath);
                File.Move(tempFile, Path.Combine(server.InstallPath, fileName), overwrite: true);
                tempFile = null; // Don't delete - we moved it
            }
        }
        finally
        {
            if (tempFile is not null && File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }

        // Run post-install commands
        if (installConfig.PostInstallCommands?.Count > 0)
        {
            progress?.Report(OfficialInstallProgress.Progress("Post-Install", "Running post-install commands...", 95));
            await RunPostInstallCommandsAsync(server.InstallPath, cancellationToken).ConfigureAwait(false);
        }

        progress?.Report(OfficialInstallProgress.Completed());
        return ServerInstallationResult.Succeeded(server.InstallPath, version);
    }

    private async Task RunPostInstallCommandsAsync(string installPath, CancellationToken cancellationToken)
    {
        var commands = _definition.InstallConfig?.PostInstallCommands;
        if (commands is null) return;

        foreach (var command in commands)
        {
            try
            {
                var processInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = Environment.OSVersion.Platform == PlatformID.Win32NT ? "cmd.exe" : "/bin/bash",
                    Arguments = Environment.OSVersion.Platform == PlatformID.Win32NT
                        ? $"/c {command}"
                        : $"-c \"{command}\"",
                    WorkingDirectory = installPath,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = System.Diagnostics.Process.Start(processInfo);
                if (process is not null)
                {
                    await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Post-install command failed: {Command}", command);
            }
        }
    }

    public async Task<ServerInstallationResult> UpdateServerAsync(
        GameServer server,
        string targetVersion,
        IProgress<OfficialInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // For most games, update is the same as install
        return await InstallServerAsync(server, targetVersion, progress, cancellationToken).ConfigureAwait(false);
    }
}
