using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;
using NebulaPanel.Web.Extensions;

namespace NebulaPanel.Web.Controllers;

[ApiController]
[Route("api/game-templates")]
[IgnoreAntiforgeryToken]
[Authorize]
public class GameTemplatesController(IGameTemplateService templateService) : ControllerBase
{
    private readonly IGameTemplateService _templateService = templateService;

    /// <summary>
    /// Export a game as a JSON template file.
    /// </summary>
    [HttpGet("export/{gameId:guid}")]
    public async Task<IActionResult> ExportGame(Guid gameId, [FromQuery] string? author = null, [FromQuery] string? description = null, CancellationToken cancellationToken = default)
    {
        var request = new GameTemplateExportRequest(author, description);
        var result = await _templateService.ExportGameAsync(gameId, request, cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
            return result.ToActionResult();

        var bytes = System.Text.Encoding.UTF8.GetBytes(result.Value!);
        return File(bytes, "application/json", $"game-template.json");
    }

    /// <summary>
    /// Import a game from a JSON template.
    /// </summary>
    [HttpPost("import")]
    public async Task<IActionResult> ImportTemplate(CancellationToken cancellationToken)
    {
        string json;

        if (Request.ContentType?.Contains("multipart/form-data") == true)
        {
            var file = Request.Form.Files.FirstOrDefault();
            if (file is null || file.Length == 0)
                return BadRequest(new { error = "No file uploaded." });

            using var reader = new StreamReader(file.OpenReadStream());
            json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            using var reader = new StreamReader(Request.Body);
            json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }

        var result = await _templateService.ImportTemplateAsync(json, cancellationToken).ConfigureAwait(false);
        return result.ToCreatedResult();
    }

    /// <summary>
    /// Validate a JSON template without importing.
    /// </summary>
    [HttpPost("validate")]
    public async Task<IActionResult> ValidateTemplate(CancellationToken cancellationToken)
    {
        string json;

        if (Request.ContentType?.Contains("multipart/form-data") == true)
        {
            var file = Request.Form.Files.FirstOrDefault();
            if (file is null || file.Length == 0)
                return BadRequest(new { error = "No file uploaded." });

            using var reader = new StreamReader(file.OpenReadStream());
            json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            using var reader = new StreamReader(Request.Body);
            json = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }

        var result = await _templateService.ValidateTemplateAsync(json, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    /// Search community templates.
    /// </summary>
    [HttpGet("community")]
    public async Task<IActionResult> SearchCommunityTemplates([FromQuery] string? query = null, CancellationToken cancellationToken = default)
    {
        var templates = await _templateService.SearchCommunityTemplatesAsync(query, cancellationToken).ConfigureAwait(false);
        return Ok(templates);
    }

    /// <summary>
    /// Get a specific community template by slug.
    /// </summary>
    [HttpGet("community/{slug}")]
    public async Task<IActionResult> GetCommunityTemplate(string slug, CancellationToken cancellationToken = default)
    {
        var result = await _templateService.GetCommunityTemplateAsync(slug, cancellationToken).ConfigureAwait(false);
        return result.ToActionResult();
    }

    /// <summary>
    /// Import a community template as a custom game.
    /// </summary>
    [HttpPost("community/{slug}/import")]
    public async Task<IActionResult> ImportCommunityTemplate(string slug, CancellationToken cancellationToken = default)
    {
        var result = await _templateService.ImportCommunityTemplateAsync(slug, cancellationToken).ConfigureAwait(false);
        return result.ToCreatedResult();
    }
}
