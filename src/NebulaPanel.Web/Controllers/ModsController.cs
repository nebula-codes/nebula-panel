using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.Repositories;

namespace NebulaPanel.Web.Controllers;

/// <summary>
/// API controller for managing server mods.
/// All endpoints are scoped to a specific game server.
/// </summary>
[ApiController]
[Route("api/servers/{serverId:guid}/mods")]
[Authorize]
public class ModsController(
    IUnifiedModService modService,
    IGameServerRepository serverRepository) : ControllerBase
{
    private readonly IUnifiedModService _modService = modService;
    private readonly IGameServerRepository _serverRepository = serverRepository;

    /// <summary>
    /// Gets all mods installed on the server.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ServerModDto>>> GetInstalled(
        Guid serverId,
        CancellationToken cancellationToken)
    {
        var server = await _serverRepository.GetByIdWithGameAsync(serverId, cancellationToken);
        if (server is null)
        {
            return NotFound(new { error = "Server not found" });
        }

        var mods = await _modService.GetInstalledAsync(server, cancellationToken);
        return Ok(mods);
    }

    /// <summary>
    /// Gets available mod providers for this server's game.
    /// </summary>
    [HttpGet("providers")]
    public async Task<ActionResult<IReadOnlyList<ModProviderInfo>>> GetProviders(
        Guid serverId,
        CancellationToken cancellationToken)
    {
        var server = await _serverRepository.GetByIdWithGameAsync(serverId, cancellationToken);
        if (server?.Game is null)
        {
            return NotFound(new { error = "Server or game not found" });
        }

        var providers = _modService.GetProvidersForGame(server.Game);
        return Ok(providers);
    }

    /// <summary>
    /// Searches for mods across all enabled providers.
    /// </summary>
    [HttpPost("search")]
    public async Task<ActionResult<UnifiedModSearchResult>> Search(
        Guid serverId,
        [FromBody] ModSearchQuery query,
        CancellationToken cancellationToken)
    {
        var server = await _serverRepository.GetByIdWithGameAsync(serverId, cancellationToken);
        if (server is null)
        {
            return NotFound(new { error = "Server not found" });
        }

        var results = await _modService.SearchAsync(server, query, cancellationToken);
        return Ok(results);
    }

    /// <summary>
    /// Searches for mods from a specific provider.
    /// </summary>
    [HttpPost("search/{provider}")]
    public async Task<ActionResult<ModSearchResult>> SearchProvider(
        Guid serverId,
        ModProviderType provider,
        [FromBody] ModSearchQuery query,
        CancellationToken cancellationToken)
    {
        var server = await _serverRepository.GetByIdWithGameAsync(serverId, cancellationToken);
        if (server is null)
        {
            return NotFound(new { error = "Server not found" });
        }

        var results = await _modService.SearchProviderAsync(server, provider, query, cancellationToken);
        return Ok(results);
    }

    /// <summary>
    /// Gets detailed information about a specific mod.
    /// </summary>
    [HttpGet("details/{provider}/{modId}")]
    public async Task<ActionResult<ModDetails>> GetDetails(
        Guid serverId,
        ModProviderType provider,
        string modId,
        CancellationToken cancellationToken)
    {
        // Verify server exists (for authorization context)
        var exists = await _serverRepository.ExistsAsync(serverId, cancellationToken);
        if (!exists)
        {
            return NotFound(new { error = "Server not found" });
        }

        var details = await _modService.GetDetailsAsync(provider, modId, cancellationToken);
        if (details is null)
        {
            return NotFound(new { error = "Mod not found" });
        }

        return Ok(details);
    }

    /// <summary>
    /// Gets available versions for a mod.
    /// </summary>
    [HttpGet("versions/{provider}/{modId}")]
    public async Task<ActionResult<IReadOnlyList<ModVersion>>> GetVersions(
        Guid serverId,
        ModProviderType provider,
        string modId,
        [FromQuery] string? gameVersion,
        CancellationToken cancellationToken)
    {
        // Verify server exists (for authorization context)
        var exists = await _serverRepository.ExistsAsync(serverId, cancellationToken);
        if (!exists)
        {
            return NotFound(new { error = "Server not found" });
        }

        var versions = await _modService.GetVersionsAsync(provider, modId, gameVersion, cancellationToken);
        return Ok(versions);
    }

    /// <summary>
    /// Checks for available updates for all installed mods.
    /// </summary>
    [HttpGet("updates")]
    public async Task<ActionResult<IReadOnlyList<ModUpdateInfo>>> CheckUpdates(
        Guid serverId,
        CancellationToken cancellationToken)
    {
        var server = await _serverRepository.GetByIdWithGameAsync(serverId, cancellationToken);
        if (server is null)
        {
            return NotFound(new { error = "Server not found" });
        }

        var updates = await _modService.CheckAllUpdatesAsync(server, cancellationToken);
        return Ok(updates);
    }

    /// <summary>
    /// Installs a mod on the server.
    /// </summary>
    [HttpPost("install")]
    public async Task<ActionResult<InstallResult>> Install(
        Guid serverId,
        [FromBody] InstallModRequest request,
        CancellationToken cancellationToken)
    {
        var server = await _serverRepository.GetByIdWithGameAsync(serverId, cancellationToken);
        if (server is null)
        {
            return NotFound(new { error = "Server not found" });
        }

        var result = await _modService.InstallAsync(
            server,
            request.Provider,
            request.ModId,
            request.VersionId,
            progress: null,
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result);
    }

    /// <summary>
    /// Toggles a mod's enabled state.
    /// </summary>
    [HttpPut("{installedModId:guid}/toggle")]
    public async Task<ActionResult<ServerModDto>> Toggle(
        Guid serverId,
        Guid installedModId,
        CancellationToken cancellationToken)
    {
        var server = await _serverRepository.GetByIdWithGameAsync(serverId, cancellationToken);
        if (server is null)
        {
            return NotFound(new { error = "Server not found" });
        }

        var result = await _modService.ToggleAsync(server, installedModId, cancellationToken: cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Updates an installed mod to the latest version.
    /// </summary>
    [HttpPost("{installedModId:guid}/update")]
    public async Task<ActionResult<InstallResult>> Update(
        Guid serverId,
        Guid installedModId,
        CancellationToken cancellationToken)
    {
        var server = await _serverRepository.GetByIdWithGameAsync(serverId, cancellationToken);
        if (server is null)
        {
            return NotFound(new { error = "Server not found" });
        }

        var result = await _modService.UpdateAsync(
            server,
            installedModId,
            progress: null,
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { error = result.Error });
        }

        return Ok(result);
    }

    /// <summary>
    /// Uninstalls a mod from the server.
    /// </summary>
    [HttpDelete("{installedModId:guid}")]
    public async Task<ActionResult> Uninstall(
        Guid serverId,
        Guid installedModId,
        CancellationToken cancellationToken)
    {
        var server = await _serverRepository.GetByIdWithGameAsync(serverId, cancellationToken);
        if (server is null)
        {
            return NotFound(new { error = "Server not found" });
        }

        var result = await _modService.UninstallAsync(server, installedModId, cancellationToken);

        if (result.IsFailure)
        {
            return BadRequest(new { error = result.Error });
        }

        return NoContent();
    }
}
