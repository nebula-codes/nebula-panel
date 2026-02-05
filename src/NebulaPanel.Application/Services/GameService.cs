using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.Repositories;

namespace NebulaPanel.Application.Services;

public class GameService(
    IGameRepository gameRepository,
    IImageService imageService,
    IOfficialGameRegistry officialGameRegistry,
    ILogger<GameService> logger) : IGameService
{
    private readonly IGameRepository _gameRepository = gameRepository;
    private readonly IImageService _imageService = imageService;
    private readonly IOfficialGameRegistry _officialGameRegistry = officialGameRegistry;
    private readonly ILogger<GameService> _logger = logger;

    public async Task<IReadOnlyList<GameListItemDto>> GetAllGamesAsync(CancellationToken cancellationToken = default)
    {
        var games = await _gameRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<GameListItemDto>();

        foreach (var game in games)
        {
            var serverCount = await _gameRepository.GetServerCountAsync(game.Id, cancellationToken).ConfigureAwait(false);
            result.Add(MapToListItemDto(game, serverCount));
        }

        return result;
    }

    public async Task<Result<GameDto>> GetGameByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var game = await _gameRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (game is null)
        {
            return Result.Failure<GameDto>($"Game with ID '{id}' not found.");
        }

        var serverCount = await _gameRepository.GetServerCountAsync(id, cancellationToken).ConfigureAwait(false);
        return MapToDto(game, serverCount);
    }

    public async Task<Result<GameDto>> GetGameBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var game = await _gameRepository.GetBySlugAsync(slug, cancellationToken).ConfigureAwait(false);
        if (game is null)
        {
            return Result.Failure<GameDto>($"Game with slug '{slug}' not found.");
        }

        var serverCount = await _gameRepository.GetServerCountAsync(game.Id, cancellationToken).ConfigureAwait(false);
        return MapToDto(game, serverCount);
    }

    public async Task<Result<GameDto>> CreateGameAsync(CreateGameRequest request, CancellationToken cancellationToken = default)
    {
        if (await _gameRepository.SlugExistsAsync(request.Slug, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<GameDto>($"A game with slug '{request.Slug}' already exists.");
        }

        // Download icon if it's a URL
        var iconPath = request.IconPath;
        if (!string.IsNullOrEmpty(iconPath) && _imageService.IsRemoteUrl(iconPath))
        {
            var localPath = await _imageService.EnsureLocalAsync(
                iconPath,
                "games",
                $"{request.Slug.ToLowerInvariant()}",
                cancellationToken).ConfigureAwait(false);

            if (localPath != null)
            {
                iconPath = localPath;
            }
            else
            {
                _logger.LogWarning("Failed to download icon for game {GameName}, using original URL", request.Name);
            }
        }

        var game = new Game
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Slug = request.Slug.ToLowerInvariant(),
            SteamAppId = request.SteamAppId,
            ExecutableType = request.ExecutableType,
            DefaultExecutablePath = request.DefaultExecutablePath,
            DefaultStartCommand = request.DefaultStartCommand,
            DefaultStopCommand = request.DefaultStopCommand,
            SupportsDocker = request.SupportsDocker,
            DefaultDockerImage = request.DefaultDockerImage,
            DockerDataPath = request.DockerDataPath,
            IconPath = iconPath,
            SupportsMods = request.SupportsMods,
            ModProviders = request.ModProviders ?? [],
            RconDefaults = request.RconDefaults,
            ConfigurationSchemas = request.ConfigurationSchemas ?? []
        };

        await _gameRepository.AddAsync(game, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Created game {GameName} with ID {GameId}", game.Name, game.Id);

        return MapToDto(game, 0);
    }

    public async Task<Result<GameDto>> UpdateGameAsync(Guid id, UpdateGameRequest request, CancellationToken cancellationToken = default)
    {
        var game = await _gameRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (game is null)
        {
            return Result.Failure<GameDto>($"Game with ID '{id}' not found.");
        }

        // Block editing official games' core definition
        if (game.SourceType == GameSourceType.Official)
        {
            return Result.Failure<GameDto>("Official games cannot have their core definition modified. Only custom games can be edited.");
        }

        if (await _gameRepository.SlugExistsAsync(request.Slug, id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<GameDto>($"A game with slug '{request.Slug}' already exists.");
        }

        // Download icon if it's a URL and different from current
        var iconPath = request.IconPath;
        if (!string.IsNullOrEmpty(iconPath) && iconPath != game.IconPath && _imageService.IsRemoteUrl(iconPath))
        {
            var localPath = await _imageService.EnsureLocalAsync(
                iconPath,
                "games",
                $"{request.Slug.ToLowerInvariant()}",
                cancellationToken).ConfigureAwait(false);

            if (localPath != null)
            {
                iconPath = localPath;
            }
            else
            {
                _logger.LogWarning("Failed to download icon for game {GameName}, using original URL", request.Name);
            }
        }

        game.Name = request.Name;
        game.Slug = request.Slug.ToLowerInvariant();
        game.SteamAppId = request.SteamAppId;
        game.ExecutableType = request.ExecutableType;
        game.DefaultExecutablePath = request.DefaultExecutablePath;
        game.DefaultStartCommand = request.DefaultStartCommand;
        game.DefaultStopCommand = request.DefaultStopCommand;
        game.SupportsDocker = request.SupportsDocker;
        game.DefaultDockerImage = request.DefaultDockerImage;
        game.DockerDataPath = request.DockerDataPath;
        game.IconPath = iconPath;
        game.SupportsMods = request.SupportsMods;
        game.ModProviders = request.ModProviders ?? [];
        game.RconDefaults = request.RconDefaults;
        game.ConfigurationSchemas = request.ConfigurationSchemas ?? [];

        await _gameRepository.UpdateAsync(game, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Updated game {GameName} with ID {GameId}", game.Name, game.Id);

        var serverCount = await _gameRepository.GetServerCountAsync(id, cancellationToken).ConfigureAwait(false);
        return MapToDto(game, serverCount);
    }

    public async Task<Result> DeleteGameAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await _gameRepository.ExistsAsync(id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure($"Game with ID '{id}' not found.");
        }

        var serverCount = await _gameRepository.GetServerCountAsync(id, cancellationToken).ConfigureAwait(false);
        if (serverCount > 0)
        {
            return Result.Failure($"Cannot delete game: {serverCount} server(s) are still using this game.");
        }

        await _gameRepository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Deleted game with ID {GameId}", id);

        return Result.Success();
    }

    public async Task<IReadOnlyList<OfficialGameListItemDto>> GetOfficialGamesAsync(CancellationToken cancellationToken = default)
    {
        var providers = _officialGameRegistry.GetAllProviders();
        var result = new List<OfficialGameListItemDto>();

        foreach (var provider in providers)
        {
            var definition = provider.GetGameDefinition();
            result.Add(new OfficialGameListItemDto(
                definition.Name,
                definition.Slug,
                definition.IconPath,
                definition.SupportsMods,
                definition.SupportsDocker
            ));
        }

        return await Task.FromResult<IReadOnlyList<OfficialGameListItemDto>>(result).ConfigureAwait(false);
    }

    public async Task<Result<IReadOnlyList<GameVersionDto>>> GetAvailableVersionsAsync(string gameSlug, CancellationToken cancellationToken = default)
    {
        var provider = _officialGameRegistry.GetProvider(gameSlug);
        if (provider is null)
        {
            return Result.Failure<IReadOnlyList<GameVersionDto>>($"No official game provider found for '{gameSlug}'.");
        }

        var versions = await provider.GetAvailableVersionsAsync(cancellationToken).ConfigureAwait(false);
        var dtos = versions.Select(v => new GameVersionDto(
            v.Version,
            v.DisplayName,
            v.ReleaseDate,
            v.VersionType,
            v.LoaderType,
            v.MinecraftVersion,
            v.IsRecommended
        )).ToList();

        return dtos;
    }

    private static GameDto MapToDto(Game game, int serverCount) => new(
        game.Id,
        game.Name,
        game.Slug,
        game.SourceType,
        game.SteamAppId,
        game.ExecutableType,
        game.DefaultExecutablePath,
        game.DefaultStartCommand,
        game.DefaultStopCommand,
        game.SupportsDocker,
        game.DefaultDockerImage,
        game.DockerDataPath,
        game.DefaultPort,
        game.IconPath,
        game.SupportsMods,
        game.ModProviders,
        game.RconDefaults,
        game.ConfigurationSchemas,
        serverCount
    );

    private static GameListItemDto MapToListItemDto(Game game, int serverCount) => new(
        game.Id,
        game.Name,
        game.Slug,
        game.SourceType,
        game.ExecutableType,
        game.SupportsDocker,
        game.SupportsMods,
        game.IconPath,
        serverCount
    );
}
