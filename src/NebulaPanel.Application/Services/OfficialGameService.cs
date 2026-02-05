using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.Repositories;

namespace NebulaPanel.Application.Services;

/// <summary>
/// Service for managing official games in the admin UI.
/// </summary>
public class OfficialGameService(
    IOfficialGameRegistry registry,
    IGameRepository gameRepository,
    IGameServerRepository serverRepository,
    IConfigurationSchemaLoader schemaLoader,
    ILogger<OfficialGameService> logger) : IOfficialGameService
{
    private readonly IOfficialGameRegistry _registry = registry;
    private readonly IGameRepository _gameRepository = gameRepository;
    private readonly IGameServerRepository _serverRepository = serverRepository;
    private readonly IConfigurationSchemaLoader _schemaLoader = schemaLoader;
    private readonly ILogger<OfficialGameService> _logger = logger;

    public async Task<IReadOnlyList<OfficialGameStatusDto>> GetAllOfficialGamesAsync(
        CancellationToken cancellationToken = default)
    {
        var statusList = new List<OfficialGameStatusDto>();

        foreach (var metadata in _registry.GetAllProviderMetadata())
        {
            // Get actual database state for enabled status and server count
            var game = await _gameRepository.GetBySlugAsync(metadata.GameSlug, cancellationToken)
                .ConfigureAwait(false);

            var serverCount = game is not null
                ? await _gameRepository.GetServerCountAsync(game.Id, cancellationToken).ConfigureAwait(false)
                : 0;

            statusList.Add(new OfficialGameStatusDto(
                Slug: metadata.GameSlug,
                Name: metadata.Name,
                IconPath: metadata.IconPath,
                ProviderType: metadata.ProviderType,
                IsEnabled: game?.IsEnabled ?? true,
                ServerCount: serverCount,
                VersionCount: game?.CachedVersionCount ?? metadata.CachedVersionCount,
                LastVersionCheck: game?.LastVersionCheck ?? metadata.LastVersionCheck,
                SchemaVersion: game?.SchemaVersion ?? metadata.SchemaVersion
            ));
        }

        return statusList.OrderBy(s => s.Name).ToList();
    }

    public async Task<Result<OfficialGameDetailDto>> GetOfficialGameAsync(
        string gameSlug,
        CancellationToken cancellationToken = default)
    {
        var provider = _registry.GetProvider(gameSlug);
        if (provider is null)
        {
            return Result.Failure<OfficialGameDetailDto>($"Official game not found: {gameSlug}");
        }

        var metadata = _registry.GetProviderMetadata(gameSlug);
        var definition = provider.GetGameDefinition();

        // Get database state
        var game = await _gameRepository.GetBySlugAsync(gameSlug, cancellationToken).ConfigureAwait(false);
        var serverCount = game is not null
            ? await _gameRepository.GetServerCountAsync(game.Id, cancellationToken).ConfigureAwait(false)
            : 0;

        // Get versions
        var versions = await provider.GetAvailableVersionsAsync(cancellationToken).ConfigureAwait(false);

        // Get schema info
        var schemaFiles = _schemaLoader.GetAvailableSchemaFiles(gameSlug);
        var schemaInfos = new List<ConfigSchemaInfoDto>();

        foreach (var schemaFile in schemaFiles)
        {
            var schema = await _schemaLoader.LoadSchemaAsync(gameSlug, schemaFile, cancellationToken)
                .ConfigureAwait(false);

            if (schema is not null)
            {
                var categories = schema.Fields
                    .Where(f => !string.IsNullOrEmpty(f.Category))
                    .Select(f => f.Category!)
                    .Distinct()
                    .ToList();

                schemaInfos.Add(new ConfigSchemaInfoDto(
                    FileName: schema.FileName,
                    FileType: schema.FileType.ToString(),
                    FieldCount: schema.Fields.Count,
                    Categories: categories
                ));
            }
        }

        // Map mod providers
        var modProviders = definition.ModProviders
            .Select(mp => new ModProviderConfigurationDto(
                Provider: mp.Provider.ToString(),
                Enabled: mp.Enabled,
                Priority: mp.Priority,
                ModInstallPath: mp.ModInstallPath
            ))
            .ToList();

        // Map RCON defaults
        RconDefaultsDto? rconDefaults = null;
        if (definition.RconDefaults is not null)
        {
            rconDefaults = new RconDefaultsDto(
                DefaultEnabled: definition.RconDefaults.DefaultEnabled,
                Protocol: definition.RconDefaults.Protocol.ToString(),
                DefaultPort: definition.RconDefaults.DefaultPort
            );
        }

        // Map recent versions (limit to 20)
        var recentVersions = versions
            .Take(20)
            .Select(v => new GameVersionDto(
                Version: v.Version,
                DisplayName: v.DisplayName,
                ReleaseDate: v.ReleaseDate,
                VersionType: v.VersionType,
                LoaderType: v.LoaderType,
                MinecraftVersion: v.MinecraftVersion,
                IsRecommended: v.IsRecommended
            ))
            .ToList();

        return Result<OfficialGameDetailDto>.Success(new OfficialGameDetailDto(
            Slug: gameSlug,
            Name: definition.Name,
            IconPath: definition.IconPath,
            ProviderType: metadata?.ProviderType ?? "Code",
            IsEnabled: game?.IsEnabled ?? true,
            ServerCount: serverCount,
            VersionCount: versions.Count,
            LastVersionCheck: game?.LastVersionCheck ?? metadata?.LastVersionCheck,
            SupportsMods: definition.SupportsMods,
            SupportsDocker: definition.SupportsDocker,
            SteamAppId: definition.SteamAppId,
            ExecutableType: definition.ExecutableType.ToString(),
            DefaultExecutablePath: definition.DefaultExecutablePath,
            DefaultStartCommand: definition.DefaultStartCommand,
            DefaultStopCommand: definition.DefaultStopCommand,
            DefaultDockerImage: definition.DefaultDockerImage,
            RconDefaults: rconDefaults,
            ModProviders: modProviders,
            ConfigSchemas: schemaInfos,
            RecentVersions: recentVersions
        ));
    }

    public async Task<Result> SetOfficialGameEnabledAsync(
        string gameSlug,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        var game = await _gameRepository.GetBySlugAsync(gameSlug, cancellationToken).ConfigureAwait(false);
        if (game is null)
        {
            return Result.Failure($"Game not found: {gameSlug}");
        }

        if (game.SourceType != Domain.Enums.GameSourceType.Official)
        {
            return Result.Failure("Cannot change enabled status of non-official games via this endpoint");
        }

        game.IsEnabled = enabled;
        await _gameRepository.UpdateAsync(game, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Set official game {GameSlug} enabled = {Enabled}", gameSlug, enabled);
        return Result.Success();
    }

    public async Task<Result<RefreshVersionsResultDto>> RefreshVersionsAsync(
        string gameSlug,
        CancellationToken cancellationToken = default)
    {
        var provider = _registry.GetProvider(gameSlug);
        if (provider is null)
        {
            return Result.Failure<RefreshVersionsResultDto>($"Official game not found: {gameSlug}");
        }

        try
        {
            _logger.LogInformation("Refreshing versions for {GameSlug}", gameSlug);

            var versions = await provider.GetAvailableVersionsAsync(cancellationToken).ConfigureAwait(false);
            var count = versions.Count;
            var refreshTime = DateTime.UtcNow;

            // Update registry metadata
            _registry.UpdateProviderMetadata(gameSlug, count, refreshTime);

            // Update database
            var game = await _gameRepository.GetBySlugAsync(gameSlug, cancellationToken).ConfigureAwait(false);
            if (game is not null)
            {
                game.CachedVersionCount = count;
                game.LastVersionCheck = refreshTime;
                await _gameRepository.UpdateAsync(game, cancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation("Refreshed {Count} versions for {GameSlug}", count, gameSlug);
            return Result<RefreshVersionsResultDto>.Success(new RefreshVersionsResultDto(count, refreshTime));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh versions for {GameSlug}", gameSlug);
            return Result.Failure<RefreshVersionsResultDto>($"Failed to refresh versions: {ex.Message}");
        }
    }

    public async Task<Result<IReadOnlyList<GameVersionDto>>> GetAvailableVersionsAsync(
        string gameSlug,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        var provider = _registry.GetProvider(gameSlug);
        if (provider is null)
        {
            return Result.Failure<IReadOnlyList<GameVersionDto>>($"Official game not found: {gameSlug}");
        }

        try
        {
            var versions = await provider.GetAvailableVersionsAsync(cancellationToken).ConfigureAwait(false);

            IEnumerable<GameVersionDto> result = versions
                .Select(v => new GameVersionDto(
                    Version: v.Version,
                    DisplayName: v.DisplayName,
                    ReleaseDate: v.ReleaseDate,
                    VersionType: v.VersionType,
                    LoaderType: v.LoaderType,
                    MinecraftVersion: v.MinecraftVersion,
                    IsRecommended: v.IsRecommended
                ));

            if (limit.HasValue)
            {
                result = result.Take(limit.Value);
            }

            return Result.Success<IReadOnlyList<GameVersionDto>>(result.ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get versions for {GameSlug}", gameSlug);
            return Result.Failure<IReadOnlyList<GameVersionDto>>($"Failed to get versions: {ex.Message}");
        }
    }

    public async Task<Result> SyncModProvidersAsync(
        string gameSlug,
        CancellationToken cancellationToken = default)
    {
        var provider = _registry.GetProvider(gameSlug);
        if (provider is null)
        {
            return Result.Failure($"Official game not found: {gameSlug}");
        }

        var game = await _gameRepository.GetBySlugAsync(gameSlug, cancellationToken).ConfigureAwait(false);
        if (game is null)
        {
            return Result.Failure($"Game not found in database: {gameSlug}");
        }

        var definition = provider.GetGameDefinition();

        // Update mod providers from definition
        game.SupportsMods = definition.SupportsMods;
        game.ModProviders = definition.ModProviders;

        await _gameRepository.UpdateAsync(game, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Synced mod providers for {GameSlug}: {ProviderCount} providers configured",
            gameSlug, definition.ModProviders.Count);

        return Result.Success();
    }

    public async Task SyncAllModProvidersAsync(CancellationToken cancellationToken = default)
    {
        foreach (var metadata in _registry.GetAllProviderMetadata())
        {
            try
            {
                var result = await SyncModProvidersAsync(metadata.GameSlug, cancellationToken).ConfigureAwait(false);
                if (!result.IsSuccess)
                {
                    _logger.LogWarning("Failed to sync mod providers for {GameSlug}: {Error}",
                        metadata.GameSlug, result.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync mod providers for {GameSlug}", metadata.GameSlug);
            }
        }
    }
}
