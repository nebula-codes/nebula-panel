using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;

namespace NebulaPanel.Application.Services;

public interface IGameService
{
    Task<IReadOnlyList<GameListItemDto>> GetAllGamesAsync(CancellationToken cancellationToken = default);
    Task<Result<GameDto>> GetGameByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<GameDto>> GetGameBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<Result<GameDto>> CreateGameAsync(CreateGameRequest request, CancellationToken cancellationToken = default);
    Task<Result<GameDto>> UpdateGameAsync(Guid id, UpdateGameRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteGameAsync(Guid id, CancellationToken cancellationToken = default);

    // Official game methods
    Task<IReadOnlyList<OfficialGameListItemDto>> GetOfficialGamesAsync(CancellationToken cancellationToken = default);
    Task<Result<IReadOnlyList<GameVersionDto>>> GetAvailableVersionsAsync(string gameSlug, CancellationToken cancellationToken = default);
}
