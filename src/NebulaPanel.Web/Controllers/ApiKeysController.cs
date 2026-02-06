using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;
using NebulaPanel.Web.Extensions;

namespace NebulaPanel.Web.Controllers;

[ApiController]
[Route("api/api-keys")]
[IgnoreAntiforgeryToken]
[Authorize]
public class ApiKeysController(IApiKeyService apiKeyService) : ControllerBase
{
    private readonly IApiKeyService _apiKeyService = apiKeyService;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        var keys = await _apiKeyService.GetByUserAsync(userId.Value, cancellationToken);
        return Ok(keys);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateApiKeyRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        var result = await _apiKeyService.CreateAsync(request, userId.Value, cancellationToken);
        return result.ToCreatedResult();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        var result = await _apiKeyService.RevokeAsync(id, userId.Value, cancellationToken);
        return result.ToNoContentResult();
    }

    private Guid? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return null;
        return userId;
    }
}
