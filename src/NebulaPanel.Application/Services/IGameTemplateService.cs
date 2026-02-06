using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;

namespace NebulaPanel.Application.Services;

public interface IGameTemplateService
{
    /// <summary>
    /// Exports an existing game as a portable JSON template.
    /// </summary>
    Task<Result<string>> ExportGameAsync(Guid gameId, GameTemplateExportRequest? request = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports a game from a JSON template, creating it as a custom game.
    /// </summary>
    Task<Result<GameDto>> ImportTemplateAsync(string json, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates a JSON template and returns any errors.
    /// </summary>
    Task<GameTemplateValidationResult> ValidateTemplateAsync(string json, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches community templates from the remote repository.
    /// </summary>
    Task<IReadOnlyList<CommunityTemplateListItemDto>> SearchCommunityTemplatesAsync(string? query = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific community template by slug.
    /// </summary>
    Task<Result<GameTemplateDto>> GetCommunityTemplateAsync(string slug, CancellationToken cancellationToken = default);

    /// <summary>
    /// Imports a community template as a custom game.
    /// </summary>
    Task<Result<GameDto>> ImportCommunityTemplateAsync(string slug, CancellationToken cancellationToken = default);
}
