using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.Repositories;

namespace NebulaPanel.Infrastructure.OfficialGames.Terraria;

/// <summary>
/// Service for managing mods on Terraria/tModLoader servers.
/// </summary>
public sealed class TerrariaModService : ITerrariaModService
{
    private const string ModsDirectory = "Mods";
    private const string EnabledJsonFile = "enabled.json";
    private const string TModExtension = ".tmod";
    private const string TModLoaderAppId = "1281930"; // tModLoader Steam App ID

    private readonly ISteamApiService _steamApi;
    private readonly IServerModRepository _modRepository;
    private readonly IEnumerable<IModProvider> _modProviders;
    private readonly ILogger<TerrariaModService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public TerrariaModService(
        ISteamApiService steamApi,
        IServerModRepository modRepository,
        IEnumerable<IModProvider> modProviders,
        ILogger<TerrariaModService> logger)
    {
        _steamApi = steamApi;
        _modRepository = modRepository;
        _modProviders = modProviders;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TerrariaInstalledMod>> GetInstalledModsAsync(
        GameServer server,
        CancellationToken cancellationToken = default)
    {
        var modsPath = GetModsPath(server);
        if (!Directory.Exists(modsPath))
        {
            _logger.LogDebug("Mods directory does not exist at {Path}", modsPath);
            return [];
        }

        // Read enabled.json to determine which mods are enabled
        var enabledMods = await ReadEnabledModsAsync(modsPath, cancellationToken).ConfigureAwait(false);

        // Get all .tmod files
        var tmodFiles = Directory.GetFiles(modsPath, $"*{TModExtension}", SearchOption.TopDirectoryOnly);

        // Get installed mods from database
        var dbMods = await _modRepository.GetByServerIdAsync(server.Id, cancellationToken).ConfigureAwait(false);
        var dbModsByFile = dbMods
            .Where(m => !string.IsNullOrEmpty(m.LocalPath))
            .ToDictionary(m => Path.GetFileName(m.LocalPath!), StringComparer.OrdinalIgnoreCase);

        var installedMods = new List<TerrariaInstalledMod>();

        foreach (var filePath in tmodFiles)
        {
            var fileName = Path.GetFileName(filePath);
            var fileInfo = new FileInfo(filePath);

            // Try to get mod name from file name (without extension)
            var modName = Path.GetFileNameWithoutExtension(fileName);

            // Check if we have this mod in the database
            dbModsByFile.TryGetValue(fileName, out var dbMod);

            // Determine if enabled based on enabled.json
            var isEnabled = enabledMods.Contains(modName, StringComparer.OrdinalIgnoreCase);

            installedMods.Add(new TerrariaInstalledMod
            {
                Id = dbMod?.Id ?? Guid.NewGuid(),
                Name = dbMod?.Name ?? modName,
                InternalName = modName,
                Version = dbMod?.Version,
                Author = dbMod?.Author,
                Description = null,
                IconUrl = dbMod?.IconUrl,
                WorkshopId = dbMod?.ModId,
                IsEnabled = isEnabled,
                FilePath = filePath,
                FileName = fileName,
                FileSizeBytes = fileInfo.Length,
                InstalledAt = dbMod?.InstalledAt ?? fileInfo.CreationTimeUtc,
                Downloads = 0
            });
        }

        return installedMods.OrderBy(m => m.Name).ToList();
    }

    /// <inheritdoc />
    public async Task<TerrariaModInstallResult> InstallModAsync(
        GameServer server,
        string workshopId,
        IProgress<TerrariaModInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Installing mod {WorkshopId} to server {ServerId}", workshopId, server.Id);

            // Get mod details from Steam
            var workshopItem = await _steamApi.GetWorkshopItemAsync(workshopId, cancellationToken).ConfigureAwait(false);
            if (workshopItem == null)
            {
                return TerrariaModInstallResult.Failed($"Workshop item {workshopId} not found");
            }

            progress?.Report(TerrariaModInstallProgress.Downloading(workshopItem.Title, 10));

            // Check if already installed
            if (await _modRepository.ExistsAsync(server.Id, ModProviderType.SteamWorkshop, workshopId, cancellationToken).ConfigureAwait(false))
            {
                return TerrariaModInstallResult.Failed("This mod is already installed");
            }

            // Find the Steam Workshop provider
            var steamProvider = _modProviders.FirstOrDefault(p => p.ProviderType == ModProviderType.SteamWorkshop);
            if (steamProvider == null)
            {
                return TerrariaModInstallResult.Failed("Steam Workshop provider not available");
            }

            // Ensure mods directory exists
            var modsPath = GetModsPath(server);
            try
            {
                Directory.CreateDirectory(modsPath);
            }
            catch (UnauthorizedAccessException)
            {
                return TerrariaModInstallResult.Failed(
                    $"Permission denied: Cannot write to '{modsPath}'. " +
                    "Ensure the application has write access to the server's install directory.");
            }

            progress?.Report(TerrariaModInstallProgress.Downloading(workshopItem.Title, 50));

            var downloadResult = await steamProvider.DownloadAsync(
                workshopId,
                workshopId, // Version ID is same as mod ID for Workshop
                modsPath,
                new Progress<DownloadProgress>(p => progress?.Report(
                    TerrariaModInstallProgress.Downloading(workshopItem.Title, (int)(p.Percentage * 0.8 + 10)))),
                cancellationToken).ConfigureAwait(false);

            if (!downloadResult.Success)
            {
                return TerrariaModInstallResult.Failed(downloadResult.Error ?? "Download failed");
            }

            progress?.Report(TerrariaModInstallProgress.Installing(workshopItem.Title));

            // Register in database
            var localPath = downloadResult.FilePath ?? Path.Combine(modsPath, $"{workshopId}{TModExtension}");
            var fileName = Path.GetFileName(localPath);
            var serverMod = new ServerMod
            {
                Id = Guid.NewGuid(),
                ServerId = server.Id,
                Provider = ModProviderType.SteamWorkshop,
                ModId = workshopId,
                Name = workshopItem.Title,
                LocalPath = localPath,
                Version = null,
                Author = workshopItem.AuthorName,
                IconUrl = workshopItem.PreviewUrl,
                IsEnabled = true,
                InstalledAt = DateTime.UtcNow,
                FileSizeBytes = workshopItem.FileSizeBytes
            };

            await _modRepository.AddAsync(serverMod, cancellationToken).ConfigureAwait(false);

            // Add to enabled.json
            var internalName = Path.GetFileNameWithoutExtension(fileName);
            await AddToEnabledModsAsync(modsPath, internalName, cancellationToken).ConfigureAwait(false);

            progress?.Report(TerrariaModInstallProgress.Complete(workshopItem.Title));

            return TerrariaModInstallResult.Successful(new TerrariaInstalledMod
            {
                Id = serverMod.Id,
                Name = workshopItem.Title,
                InternalName = internalName,
                Author = workshopItem.AuthorName,
                IconUrl = workshopItem.PreviewUrl,
                WorkshopId = workshopId,
                IsEnabled = true,
                FilePath = localPath,
                FileName = fileName,
                FileSizeBytes = workshopItem.FileSizeBytes,
                InstalledAt = serverMod.InstalledAt,
                Downloads = workshopItem.Subscriptions
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to install mod {WorkshopId} to server {ServerId}", workshopId, server.Id);
            return TerrariaModInstallResult.Failed($"Installation failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<TerrarriaModBulkInstallResult> InstallModsAsync(
        GameServer server,
        IEnumerable<string> workshopIds,
        IProgress<TerrariaModInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var idList = workshopIds.ToList();
        if (idList.Count == 0)
        {
            return TerrarriaModBulkInstallResult.Successful(0, 0);
        }

        var installed = 0;
        var skipped = 0;
        var errors = new List<string>();

        for (int i = 0; i < idList.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var workshopId = idList[i];
            progress?.Report(TerrariaModInstallProgress.BulkProgress(
                $"Installing mod {i + 1} of {idList.Count}...",
                i + 1,
                idList.Count));

            var result = await InstallModAsync(server, workshopId, null, cancellationToken).ConfigureAwait(false);

            if (result.Success)
            {
                installed++;
            }
            else if (result.Error?.Contains("already installed", StringComparison.OrdinalIgnoreCase) == true)
            {
                skipped++;
            }
            else
            {
                errors.Add($"{workshopId}: {result.Error}");
            }
        }

        return new TerrarriaModBulkInstallResult
        {
            Success = installed > 0 || skipped > 0,
            InstalledCount = installed,
            TotalCount = idList.Count,
            SkippedCount = skipped,
            FailedCount = errors.Count,
            Errors = errors
        };
    }

    /// <inheritdoc />
    public async Task<bool> UninstallModAsync(
        GameServer server,
        Guid modId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var serverMod = await _modRepository.GetByIdAsync(modId, cancellationToken).ConfigureAwait(false);
            if (serverMod == null || serverMod.ServerId != server.Id)
            {
                _logger.LogWarning("Mod {ModId} not found or doesn't belong to server {ServerId}", modId, server.Id);
                return false;
            }

            // Delete the file
            if (!string.IsNullOrEmpty(serverMod.LocalPath) && File.Exists(serverMod.LocalPath))
            {
                File.Delete(serverMod.LocalPath);
            }

            // Remove from enabled.json
            var modsPath = GetModsPath(server);
            var fileName = !string.IsNullOrEmpty(serverMod.LocalPath) ? Path.GetFileName(serverMod.LocalPath) : null;
            var internalName = !string.IsNullOrEmpty(fileName) ? Path.GetFileNameWithoutExtension(fileName) : null;
            if (!string.IsNullOrEmpty(internalName))
            {
                await RemoveFromEnabledModsAsync(modsPath, internalName, cancellationToken).ConfigureAwait(false);
            }

            // Remove from database
            await _modRepository.DeleteAsync(modId, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Uninstalled mod {ModName} ({ModId}) from server {ServerId}",
                serverMod.Name, modId, server.Id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to uninstall mod {ModId} from server {ServerId}", modId, server.Id);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> SetModEnabledAsync(
        GameServer server,
        Guid modId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var serverMod = await _modRepository.GetByIdAsync(modId, cancellationToken).ConfigureAwait(false);
            if (serverMod == null || serverMod.ServerId != server.Id)
            {
                return false;
            }

            var modsPath = GetModsPath(server);
            var fileName = !string.IsNullOrEmpty(serverMod.LocalPath) ? Path.GetFileName(serverMod.LocalPath) : null;
            var internalName = !string.IsNullOrEmpty(fileName) ? Path.GetFileNameWithoutExtension(fileName) : null;

            if (!string.IsNullOrEmpty(internalName))
            {
                if (enabled)
                {
                    await AddToEnabledModsAsync(modsPath, internalName, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await RemoveFromEnabledModsAsync(modsPath, internalName, cancellationToken).ConfigureAwait(false);
                }
            }

            // Update database
            serverMod.IsEnabled = enabled;
            await _modRepository.UpdateAsync(serverMod, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("{Action} mod {ModName} on server {ServerId}",
                enabled ? "Enabled" : "Disabled", serverMod.Name, server.Id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to {Action} mod {ModId} on server {ServerId}",
                enabled ? "enable" : "disable", modId, server.Id);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<TerrarriaModBulkInstallResult> ImportModpackAsync(
        GameServer server,
        string json,
        IProgress<TerrariaModInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var workshopIds = ParseModpackJson(json);
            if (workshopIds.Count == 0)
            {
                return TerrarriaModBulkInstallResult.Failed("No valid mod IDs found in the modpack");
            }

            return await InstallModsAsync(server, workshopIds, progress, cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse modpack JSON");
            return TerrarriaModBulkInstallResult.Failed($"Invalid JSON format: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<string> ExportModpackAsync(
        GameServer server,
        CancellationToken cancellationToken = default)
    {
        var installedMods = await GetInstalledModsAsync(server, cancellationToken).ConfigureAwait(false);

        var modpack = new TerrariaModpackJson
        {
            Name = $"{server.Name} Modpack",
            Mods = installedMods
                .Where(m => !string.IsNullOrEmpty(m.WorkshopId))
                .Select(m => new TerrariaModpackEntry
                {
                    WorkshopId = m.WorkshopId!,
                    Name = m.Name
                })
                .ToList()
        };

        return JsonSerializer.Serialize(modpack, JsonOptions);
    }

    /// <inheritdoc />
    public async Task<TerrarriaModBulkInstallResult> ImportCollectionAsync(
        GameServer server,
        string collectionUrl,
        IProgress<TerrariaModInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var collectionId = ExtractCollectionId(collectionUrl);
            if (string.IsNullOrEmpty(collectionId))
            {
                return TerrarriaModBulkInstallResult.Failed("Invalid collection URL or ID");
            }

            progress?.Report(new TerrariaModInstallProgress
            {
                Phase = "Loading",
                Message = "Fetching collection details...",
                Percentage = 5
            });

            var collection = await _steamApi.GetCollectionAsync(collectionId, cancellationToken).ConfigureAwait(false);
            if (collection == null)
            {
                return TerrarriaModBulkInstallResult.Failed($"Collection {collectionId} not found");
            }

            if (collection.Items.Count == 0)
            {
                return TerrarriaModBulkInstallResult.Failed("Collection contains no items");
            }

            var workshopIds = collection.Items.Select(i => i.Id).ToList();
            return await InstallModsAsync(server, workshopIds, progress, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import collection {CollectionUrl}", collectionUrl);
            return TerrarriaModBulkInstallResult.Failed($"Failed to import collection: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<ModSearchResult> SearchAsync(
        ModSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = await _steamApi.SearchWorkshopItemsAsync(
            TModLoaderAppId,
            query.Query,
            query.Page,
            query.PageSize,
            cancellationToken).ConfigureAwait(false);

        var mods = result.Items.Select(item => new ModSearchItem(
            Id: item.Id,
            Slug: item.Id,
            Name: item.Title,
            Summary: TruncateDescription(item.Description),
            IconUrl: item.PreviewUrl,
            Author: item.AuthorName,
            Downloads: item.Subscriptions,
            UpdatedAt: item.UpdatedAt,
            Categories: item.Tags.ToList(),
            GameVersions: [],
            Provider: ModProviderType.SteamWorkshop
        )).ToList();

        var totalPages = result.TotalCount > 0 && query.PageSize > 0
            ? (int)Math.Ceiling((double)result.TotalCount / query.PageSize)
            : 0;

        return new ModSearchResult(mods, result.TotalCount, query.Page, query.PageSize, totalPages);
    }

    /// <inheritdoc />
    public async Task<ModDetails?> GetModDetailsAsync(
        string workshopId,
        CancellationToken cancellationToken = default)
    {
        var item = await _steamApi.GetWorkshopItemAsync(workshopId, cancellationToken).ConfigureAwait(false);
        if (item == null)
        {
            return null;
        }

        return new ModDetails(
            Id: item.Id,
            Slug: item.Id,
            Name: item.Title,
            Summary: TruncateDescription(item.Description),
            Description: item.Description,
            IconUrl: item.PreviewUrl,
            BannerUrl: item.PreviewUrl,
            PageUrl: $"https://steamcommunity.com/sharedfiles/filedetails/?id={item.Id}",
            SourceUrl: null,
            WikiUrl: null,
            DiscordUrl: null,
            Authors: [new ModAuthor(item.AuthorName ?? "Unknown", null)],
            Downloads: item.Subscriptions,
            CreatedAt: item.CreatedAt,
            UpdatedAt: item.UpdatedAt,
            Categories: item.Tags.ToList(),
            GameVersions: [],
            Loaders: ["tModLoader"],
            Screenshots: [],
            Dependencies: [],
            Provider: ModProviderType.SteamWorkshop
        );
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TerrariaModPreview>> PreviewModpackAsync(
        string json,
        CancellationToken cancellationToken = default)
    {
        var workshopIds = ParseModpackJson(json);
        if (workshopIds.Count == 0)
        {
            return [];
        }

        var items = await _steamApi.GetWorkshopItemsAsync(workshopIds, cancellationToken).ConfigureAwait(false);

        return items.Select(item => new TerrariaModPreview
        {
            WorkshopId = item.Id,
            Name = item.Title,
            Author = item.AuthorName,
            Description = TruncateDescription(item.Description),
            IconUrl = item.PreviewUrl,
            FileSizeBytes = item.FileSizeBytes,
            Downloads = item.Subscriptions,
            AlreadyInstalled = false // Will be checked by the UI
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<TerrariaCollectionPreview?> PreviewCollectionAsync(
        string collectionUrl,
        CancellationToken cancellationToken = default)
    {
        var collectionId = ExtractCollectionId(collectionUrl);
        if (string.IsNullOrEmpty(collectionId))
        {
            return null;
        }

        var collection = await _steamApi.GetCollectionAsync(collectionId, cancellationToken).ConfigureAwait(false);
        if (collection == null)
        {
            return null;
        }

        return new TerrariaCollectionPreview
        {
            Id = collection.Id,
            Title = collection.Title,
            Description = collection.Description,
            PreviewUrl = collection.PreviewUrl,
            ModCount = collection.ItemCount,
            Mods = collection.Items.Select(item => new TerrariaModPreview
            {
                WorkshopId = item.Id,
                Name = item.Title,
                Author = item.AuthorName,
                Description = TruncateDescription(item.Description),
                IconUrl = item.PreviewUrl,
                FileSizeBytes = item.FileSizeBytes,
                Downloads = item.Subscriptions,
                AlreadyInstalled = false
            }).ToList()
        };
    }

    #region Helper Methods

    private static string GetModsPath(GameServer server)
    {
        return Path.Combine(server.InstallPath, ModsDirectory);
    }

    private static async Task<HashSet<string>> ReadEnabledModsAsync(string modsPath, CancellationToken cancellationToken)
    {
        var enabledPath = Path.Combine(modsPath, EnabledJsonFile);
        if (!File.Exists(enabledPath))
        {
            return [];
        }

        try
        {
            var json = await File.ReadAllTextAsync(enabledPath, cancellationToken).ConfigureAwait(false);
            var enabledList = JsonSerializer.Deserialize<List<string>>(json);
            return enabledList != null ? new HashSet<string>(enabledList, StringComparer.OrdinalIgnoreCase) : [];
        }
        catch
        {
            return [];
        }
    }

    private static async Task WriteEnabledModsAsync(string modsPath, HashSet<string> enabledMods, CancellationToken cancellationToken)
    {
        var enabledPath = Path.Combine(modsPath, EnabledJsonFile);
        var json = JsonSerializer.Serialize(enabledMods.ToList(), JsonOptions);
        await File.WriteAllTextAsync(enabledPath, json, cancellationToken).ConfigureAwait(false);
    }

    private static async Task AddToEnabledModsAsync(string modsPath, string modName, CancellationToken cancellationToken)
    {
        var enabledMods = await ReadEnabledModsAsync(modsPath, cancellationToken).ConfigureAwait(false);
        if (enabledMods.Add(modName))
        {
            await WriteEnabledModsAsync(modsPath, enabledMods, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task RemoveFromEnabledModsAsync(string modsPath, string modName, CancellationToken cancellationToken)
    {
        var enabledMods = await ReadEnabledModsAsync(modsPath, cancellationToken).ConfigureAwait(false);
        if (enabledMods.Remove(modName))
        {
            await WriteEnabledModsAsync(modsPath, enabledMods, cancellationToken).ConfigureAwait(false);
        }
    }

    private static List<string> ParseModpackJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Handle array format: ["id1", "id2", ...]
        if (root.ValueKind == JsonValueKind.Array)
        {
            return root.EnumerateArray()
                .Select(e => e.GetString())
                .Where(s => !string.IsNullOrEmpty(s))
                .Cast<string>()
                .ToList();
        }

        // Handle object format: { "mods": [...] }
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("mods", out var modsArray))
        {
            return modsArray.EnumerateArray()
                .Select(e =>
                {
                    if (e.ValueKind == JsonValueKind.String)
                    {
                        return e.GetString();
                    }
                    if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty("workshopId", out var idProp))
                    {
                        return idProp.GetString();
                    }
                    return null;
                })
                .Where(s => !string.IsNullOrEmpty(s))
                .Cast<string>()
                .ToList();
        }

        return [];
    }

    private static string? ExtractCollectionId(string urlOrId)
    {
        if (string.IsNullOrWhiteSpace(urlOrId))
        {
            return null;
        }

        // If it's just a number, return it directly
        if (long.TryParse(urlOrId.Trim(), out _))
        {
            return urlOrId.Trim();
        }

        // Try to extract ID from URL
        // Format: https://steamcommunity.com/sharedfiles/filedetails/?id=123456789
        var match = Regex.Match(urlOrId, @"[?&]id=(\d+)");
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        return null;
    }

    private static string? TruncateDescription(string? description)
    {
        if (string.IsNullOrEmpty(description))
        {
            return null;
        }

        const int maxLength = 200;
        if (description.Length <= maxLength)
        {
            return description;
        }

        return description[..maxLength].TrimEnd() + "...";
    }

    #endregion
}
