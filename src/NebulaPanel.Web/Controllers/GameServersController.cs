using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;
using NebulaPanel.Web.Extensions;

namespace NebulaPanel.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[IgnoreAntiforgeryToken]
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
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _serverService.GetServerByIdAsync(id, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateGameServerRequest request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await _serverService.CreateServerAsync(request, userId.Value, cancellationToken);
        return result.ToCreatedResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateGameServerRequest request, CancellationToken cancellationToken)
    {
        var result = await _serverService.UpdateServerAsync(id, request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromQuery] bool deleteFiles = false,
        [FromQuery] bool deleteContainer = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _serverService.DeleteServerAsync(id, deleteFiles, deleteContainer, cancellationToken);
        return result.ToNoContentResult();
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
