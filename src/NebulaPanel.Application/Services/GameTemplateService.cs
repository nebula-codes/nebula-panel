using System.Text.Json;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.Repositories;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Application.Services;

public class GameTemplateService(
    IGameRepository gameRepository,
    IGameService gameService,
    ICommunityTemplateRepository communityRepository,
    ILogger<GameTemplateService> logger) : IGameTemplateService
{
    private readonly IGameRepository _gameRepository = gameRepository;
    private readonly IGameService _gameService = gameService;
    private readonly ICommunityTemplateRepository _communityRepository = communityRepository;
    private readonly ILogger<GameTemplateService> _logger = logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public async Task<Result<string>> ExportGameAsync(Guid gameId, GameTemplateExportRequest? request = null, CancellationToken cancellationToken = default)
    {
        var game = await _gameRepository.GetByIdAsync(gameId, cancellationToken).ConfigureAwait(false);
        if (game is null)
            return Result.Failure<string>(Error.NotFound("Game", gameId.ToString()));

        var template = GameTemplate.FromGame(
            game,
            request?.Author,
            request?.Description,
            request?.Tags);

        var json = JsonSerializer.Serialize(template, JsonOptions);
        _logger.LogInformation("Exported game template for {GameName} ({GameSlug})", game.Name, game.Slug);

        return json;
    }

    public async Task<Result<GameDto>> ImportTemplateAsync(string json, CancellationToken cancellationToken = default)
    {
        var validation = await ValidateTemplateAsync(json, cancellationToken).ConfigureAwait(false);
        if (!validation.IsValid)
            return Result.Failure<GameDto>(Error.Validation(string.Join("; ", validation.Errors)));

        var template = JsonSerializer.Deserialize<GameTemplate>(json, JsonOptions)!;
        return await ImportDefinitionAsync(template.Definition, cancellationToken).ConfigureAwait(false);
    }

    public Task<GameTemplateValidationResult> ValidateTemplateAsync(string json, CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        GameTemplate? template;
        try
        {
            template = JsonSerializer.Deserialize<GameTemplate>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            errors.Add($"Invalid JSON: {ex.Message}");
            return Task.FromResult(new GameTemplateValidationResult(false, errors));
        }

        if (template is null)
        {
            errors.Add("Template is empty.");
            return Task.FromResult(new GameTemplateValidationResult(false, errors));
        }

        if (template.Definition is null)
        {
            errors.Add("Template must contain a 'definition' object.");
            return Task.FromResult(new GameTemplateValidationResult(false, errors));
        }

        var def = template.Definition;

        if (string.IsNullOrWhiteSpace(def.Slug))
            errors.Add("Definition slug is required.");

        if (string.IsNullOrWhiteSpace(def.Name))
            errors.Add("Definition name is required.");

        if (string.IsNullOrWhiteSpace(def.DefaultExecutablePath))
            errors.Add("Default executable path is required.");

        if (string.IsNullOrWhiteSpace(def.DefaultStartCommand))
            errors.Add("Default start command is required.");

        return Task.FromResult(new GameTemplateValidationResult(errors.Count == 0, errors));
    }

    public async Task<IReadOnlyList<CommunityTemplateListItemDto>> SearchCommunityTemplatesAsync(string? query = null, CancellationToken cancellationToken = default)
    {
        var templates = await _communityRepository.SearchAsync(query, cancellationToken).ConfigureAwait(false);
        return templates.Select(t => new CommunityTemplateListItemDto(
            t.Slug, t.Name, t.Author, t.Description, t.Tags, t.UpdatedAt
        )).ToList();
    }

    public async Task<Result<GameTemplateDto>> GetCommunityTemplateAsync(string slug, CancellationToken cancellationToken = default)
    {
        var template = await _communityRepository.GetBySlugAsync(slug, cancellationToken).ConfigureAwait(false);
        if (template is null)
            return Result.Failure<GameTemplateDto>(Error.NotFound("Community template", slug));

        return new GameTemplateDto(
            template.Definition.Slug,
            template.Definition.Name,
            template.Definition.ExecutableType,
            template.Definition.SupportsDocker,
            template.Definition.SupportsMods,
            template.Author,
            template.Description,
            template.Tags,
            template.TemplateVersion,
            template.CreatedAt);
    }

    public async Task<Result<GameDto>> ImportCommunityTemplateAsync(string slug, CancellationToken cancellationToken = default)
    {
        var template = await _communityRepository.GetBySlugAsync(slug, cancellationToken).ConfigureAwait(false);
        if (template is null)
            return Result.Failure<GameDto>(Error.NotFound("Community template", slug));

        return await ImportDefinitionAsync(template.Definition, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<GameDto>> ImportDefinitionAsync(JsonGameDefinition definition, CancellationToken cancellationToken)
    {
        var request = new CreateGameRequest(
            definition.Name,
            definition.Slug,
            definition.SteamAppId,
            definition.ExecutableType,
            definition.DefaultExecutablePath,
            definition.DefaultStartCommand,
            definition.DefaultStopCommand,
            definition.SupportsDocker,
            definition.DefaultDockerImage,
            null, // DockerDataPath
            definition.IconPath,
            definition.SupportsMods,
            definition.ModProviders.Count > 0 ? definition.ModProviders : null,
            definition.RconDefaults,
            null // ConfigurationSchemas — not carried in simple template
        );

        var result = await _gameService.CreateGameAsync(request, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            _logger.LogInformation("Imported game from template: {GameName} ({GameSlug})", definition.Name, definition.Slug);
        }

        return result;
    }
}
