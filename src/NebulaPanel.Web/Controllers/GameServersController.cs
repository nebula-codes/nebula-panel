using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;

namespace NebulaPanel.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GameServersController(IGameServerService serverService) : ControllerBase
{
    private readonly IGameServerService _serverService = serverService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<GameServerListItemDto>>> GetAll(CancellationToken cancellationToken)
    {
        var servers = await _serverService.GetAllServersAsync(cancellationToken);
        return Ok(servers);
    }

    [HttpGet("mine")]
    public async Task<ActionResult<IReadOnlyList<GameServerListItemDto>>> GetMyServers(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var servers = await _serverService.GetServersByOwnerAsync(userId.Value, cancellationToken);
        return Ok(servers);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<GameServerDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _serverService.GetServerByIdAsync(id, cancellationToken);

        if (result.IsFailure)
        {
            return NotFound(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    [HttpPost]
    public async Task<ActionResult<GameServerDto>> Create([FromBody] CreateGameServerRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _serverService.CreateServerAsync(request, userId.Value, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<GameServerDto>> Update(Guid id, [FromBody] UpdateGameServerRequest request, CancellationToken cancellationToken)
    {
        var result = await _serverService.UpdateServerAsync(id, request, cancellationToken);

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
    public async Task<ActionResult> Delete(
        Guid id,
        [FromQuery] bool deleteFiles = false,
        [FromQuery] bool deleteContainer = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _serverService.DeleteServerAsync(id, deleteFiles, deleteContainer, cancellationToken);

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

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return null;
        }
        return userId;
    }
}
