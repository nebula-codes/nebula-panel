using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;
using NebulaPanel.Web.Extensions;

namespace NebulaPanel.Web.Controllers;

[ApiController]
[Route("api/alert-rules")]
[IgnoreAntiforgeryToken]
[Authorize]
public class AlertRulesController(IAlertRuleService alertRuleService) : ControllerBase
{
    private readonly IAlertRuleService _alertRuleService = alertRuleService;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        var rules = await _alertRuleService.GetByOwnerAsync(userId.Value, cancellationToken);
        return Ok(rules);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _alertRuleService.GetByIdAsync(id, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateAlertRuleRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        var result = await _alertRuleService.CreateAsync(request, userId.Value, cancellationToken);
        return result.ToCreatedResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateAlertRuleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _alertRuleService.UpdateAsync(id, request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _alertRuleService.DeleteAsync(id, cancellationToken);
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
