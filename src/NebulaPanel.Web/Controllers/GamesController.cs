using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;

namespace NebulaPanel.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[IgnoreAntiforgeryToken]
[Authorize]
public class GamesController(IGameService gameService) : ControllerBase
{
    private readonly IGameService _gameService = gameService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GameListItemDto>>> GetAll(CancellationToken cancellationToken)
    {
        var games = await _gameService.GetAllGamesAsync(cancellationToken);
        return Ok(games);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GameDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _gameService.GetGameByIdAsync(id, cancellationToken);

        if (result.IsFailure)
        {
            return NotFound(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    [HttpGet("by-slug/{slug}")]
    public async Task<ActionResult<GameDto>> GetBySlug(string slug, CancellationToken cancellationToken)
    {
        var result = await _gameService.GetGameBySlugAsync(slug, cancellationToken);

        if (result.IsFailure)
        {
            return NotFound(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<ActionResult<GameDto>> Create([FromBody] CreateGameRequest request, CancellationToken cancellationToken)
    {
        var result = await _gameService.CreateGameAsync(request, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<GameDto>> Update(Guid id, [FromBody] UpdateGameRequest request, CancellationToken cancellationToken)
    {
        var result = await _gameService.UpdateGameAsync(id, request, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error!.Contains("not found"))
            {
                return NotFound(new { error = result.Error });
            }
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _gameService.DeleteGameAsync(id, cancellationToken);

        if (result.IsFailure)
        {
            if (result.Error!.Contains("not found"))
            {
                return NotFound(new { error = result.Error });
            }
            return BadRequest(new { error = result.Error });
        }

        return NoContent();
    }
}
