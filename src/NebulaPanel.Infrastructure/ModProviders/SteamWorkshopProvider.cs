using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;

namespace NebulaPanel.Infrastructure.ModProviders;

/// <summary>
/// Steam Workshop mod provider implementation.
/// Uses Steam Web API for searching and SteamCMD for downloading.
/// </summary>
public sealed class SteamWorkshopProvider : IModProvider
{
    private readonly ISteamApiService _steamApiService;
    private readonly ISteamCmdService _steamCmdService;
    private readonly ILogger<SteamWorkshopProvider> _logger;

    public SteamWorkshopProvider(
        ISteamApiService steamApiService,
        ISteamCmdService steamCmdService,
        ILogger<SteamWorkshopProvider> logger)
    {
        _steamApiService = steamApiService;
        _steamCmdService = steamCmdService;
        _logger = logger;
    }

    /// <inheritdoc />
    public ModProviderType ProviderType => ModProviderType.SteamWorkshop;

    /// <inheritdoc />
    public string DisplayName => "Steam Workshop";

    /// <inheritdoc />
    public string? IconUrl => "https://store.steampowered.com/favicon.ico";

    /// <inheritdoc />
    public Task<bool> SupportsGameAsync(string gameSlug, CancellationToken cancellationToken = default)
    {
        // Steam Workshop supports any game with a Steam App ID that has Workshop enabled
        // For now, we'll return true and let individual operations fail if Workshop isn't available
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public async Task<ModSearchResult> SearchAsync(
        ModSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query.GameSlug))
        {
            _logger.LogWarning("Steam Workshop search requires a game slug (Steam App ID)");
            return new ModSearchResult([], 0, query.Page, query.PageSize, 0);
        }

        try
        {
            var result = await _steamApiService.SearchWorkshopItemsAsync(
                query.GameSlug,
                query.Query,
                query.Page,
                query.PageSize,
                cancellationToken).ConfigureAwait(false);

            var mods = result.Items.Select(item => new ModSearchItem(
                Id: item.Id,
                Slug: item.Id,
                Name: item.Title,
                Summary: TruncateDescription(item.Description, 200),
                IconUrl: item.PreviewUrl,
                Author: item.AuthorName,
                Downloads: item.Subscriptions,
                UpdatedAt: item.UpdatedAt,
                Categories: item.Tags.ToList(),
                GameVersions: [],
                Provider: ModProviderType.SteamWorkshop
            )).ToList();

            var totalPages = result.TotalCount > 0
                ? (int)Math.Ceiling((double)result.TotalCount / query.PageSize)
                : 0;

            return new ModSearchResult(
                Mods: mods,
                TotalCount: result.TotalCount,
                Page: query.Page,
                PageSize: query.PageSize,
                TotalPages: totalPages
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search Steam Workshop for app {AppId}", query.GameSlug);
            return new ModSearchResult([], 0, query.Page, query.PageSize, 0);
        }
    }

    /// <inheritdoc />
    public async Task<ModDetails?> GetDetailsAsync(
        string modId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var item = await _steamApiService.GetWorkshopItemAsync(modId, cancellationToken).ConfigureAwait(false);

            if (item is null)
            {
                return null;
            }

            return new ModDetails(
                Id: item.Id,
                Slug: item.Id,
                Name: item.Title,
                Summary: TruncateDescription(item.Description, 200),
                Description: item.Description,
                IconUrl: item.PreviewUrl,
                BannerUrl: item.PreviewUrl,
                PageUrl: $"https://steamcommunity.com/sharedfiles/filedetails/?id={item.Id}",
                SourceUrl: null,
                WikiUrl: null,
                DiscordUrl: null,
                Authors: [new ModAuthor(item.AuthorName ?? "Unknown", $"https://steamcommunity.com/profiles/{item.AuthorSteamId}")],
                Downloads: item.Subscriptions,
                CreatedAt: item.CreatedAt,
                UpdatedAt: item.UpdatedAt,
                Categories: item.Tags.ToList(),
                GameVersions: [],
                Loaders: [],
                Screenshots: item.PreviewUrl is not null
                    ? [new ModScreenshot(item.PreviewUrl, null)]
                    : [],
                Dependencies: [],
                Provider: ModProviderType.SteamWorkshop
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Workshop item {ModId}", modId);
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModVersion>> GetVersionsAsync(
        string modId,
        string? gameVersion,
        CancellationToken cancellationToken = default)
    {
        // Steam Workshop items don't have traditional versioning like Modrinth/CurseForge
        // They have a single "current" version based on the last update
        var item = await _steamApiService.GetWorkshopItemAsync(modId, cancellationToken).ConfigureAwait(false);

        if (item is null)
        {
            return [];
        }

        // Create a single version entry representing the current Workshop state
        return
        [
            new ModVersion(
                Id: item.Id, // Use the Workshop item ID as the version ID
                Version: item.UpdatedAt.ToString("yyyy.MM.dd"),
                Name: $"Update {item.UpdatedAt:yyyy-MM-dd}",
                Changelog: null,
                GameVersions: [],
                Loaders: [],
                ReleasedAt: item.UpdatedAt,
                Downloads: item.Subscriptions,
                VersionType: ModVersionType.Release,
                Dependencies: [],
                Files:
                [
                    new ModFile(
                        FileName: $"{item.Id}.workshop",
                        Url: $"steam://workshop/{item.Id}",
                        SizeBytes: item.FileSizeBytes,
                        Sha512: null,
                        Sha1: null,
                        IsPrimary: true
                    )
                ]
            )
        ];
    }

    /// <inheritdoc />
    public async Task<ModDownloadResult> DownloadAsync(
        string modId,
        string versionId,
        string destinationPath,
        IProgress<DownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get the Workshop item to find the App ID
            var item = await _steamApiService.GetWorkshopItemAsync(modId, cancellationToken).ConfigureAwait(false);

            if (item is null)
            {
                return new ModDownloadResult(false, null, $"Workshop item {modId} not found", null);
            }

            if (string.IsNullOrEmpty(item.AppId))
            {
                return new ModDownloadResult(false, null, "Could not determine game App ID for Workshop item", null);
            }

            _logger.LogInformation(
                "Downloading Workshop item {ItemId} for app {AppId} to {Destination}",
                modId, item.AppId, destinationPath);

            // Use SteamCMD to download the Workshop item
            // SteamCMD command: +workshop_download_item <app_id> <workshop_item_id>
            var downloadProgress = new Progress<SteamCmdProgress>(p =>
            {
                progress?.Report(new DownloadProgress(
                    BytesDownloaded: (long)(p.PercentComplete * item.FileSizeBytes / 100),
                    TotalBytes: item.FileSizeBytes,
                    Percentage: p.PercentComplete
                ));
            });

            var result = await _steamCmdService.DownloadWorkshopItemAsync(
                item.AppId,
                modId,
                destinationPath,
                downloadProgress,
                cancellationToken).ConfigureAwait(false);

            if (!result.Success)
            {
                return new ModDownloadResult(false, null, result.Error ?? "Download failed", null);
            }

            // Workshop items are downloaded to: <destinationPath>/steamapps/workshop/content/<appid>/<workshopid>/
            var workshopPath = Path.Combine(
                destinationPath,
                "steamapps", "workshop", "content",
                item.AppId,
                modId);

            // Find the actual downloaded files
            if (!Directory.Exists(workshopPath))
            {
                // Try alternative path structure
                workshopPath = Path.Combine(destinationPath, modId);
                if (!Directory.Exists(workshopPath))
                {
                    return new ModDownloadResult(
                        true,
                        destinationPath,
                        null,
                        null);
                }
            }

            return new ModDownloadResult(
                true,
                workshopPath,
                null,
                null
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download Workshop item {ModId}", modId);
            return new ModDownloadResult(false, null, $"Download failed: {ex.Message}", null);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ModUpdateInfo>> CheckUpdatesAsync(
        IEnumerable<InstalledModInfo> installedMods,
        string? gameVersion,
        CancellationToken cancellationToken = default)
    {
        var updates = new List<ModUpdateInfo>();
        var modsToCheck = installedMods
            .Where(m => m.Provider == ModProviderType.SteamWorkshop)
            .ToList();

        foreach (var mod in modsToCheck)
        {
            try
            {
                var item = await _steamApiService.GetWorkshopItemAsync(mod.ModId, cancellationToken).ConfigureAwait(false);

                if (item is null)
                {
                    continue;
                }

                // Check if the Workshop item has been updated since the installed version
                // For Workshop items, we use the update timestamp as the version
                var currentVersionDate = TryParseVersionDate(mod.VersionId);
                if (currentVersionDate.HasValue && item.UpdatedAt > currentVersionDate.Value)
                {
                    updates.Add(new ModUpdateInfo(
                        InstalledModId: Guid.Empty, // Will be set by the service layer
                        ModName: item.Title,
                        CurrentVersion: mod.VersionId,
                        LatestVersion: item.UpdatedAt.ToString("yyyy.MM.dd"),
                        LatestVersionId: item.Id,
                        ReleasedAt: item.UpdatedAt,
                        Changelog: null,
                        Provider: ModProviderType.SteamWorkshop
                    ));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to check updates for Workshop item {ModId}", mod.ModId);
            }
        }

        return updates;
    }

    private static string? TruncateDescription(string? description, int maxLength)
    {
        if (string.IsNullOrEmpty(description))
            return null;

        if (description.Length <= maxLength)
            return description;

        return description[..(maxLength - 3)] + "...";
    }

    private static DateTime? TryParseVersionDate(string versionId)
    {
        // Try parsing as our version format (yyyy.MM.dd)
        if (DateTime.TryParseExact(
            versionId,
            "yyyy.MM.dd",
            null,
            System.Globalization.DateTimeStyles.None,
            out var date))
        {
            return date;
        }

        // Try parsing as Workshop item ID (which is just a number)
        // In this case, we can't determine the version date
        return null;
    }
}
