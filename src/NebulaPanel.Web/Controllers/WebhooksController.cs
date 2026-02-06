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
public class WebhooksController(IWebhookService webhookService) : ControllerBase
{
    private readonly IWebhookService _webhookService = webhookService;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        var endpoints = await _webhookService.GetByOwnerAsync(userId.Value, cancellationToken);
        return Ok(endpoints);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _webhookService.GetByIdAsync(id, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateWebhookEndpointRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
            return Unauthorized();

        var result = await _webhookService.CreateAsync(request, userId.Value, cancellationToken);
        return result.ToCreatedResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateWebhookEndpointRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _webhookService.UpdateAsync(id, request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _webhookService.DeleteAsync(id, cancellationToken);
        return result.ToNoContentResult();
    }

    [HttpGet("{id:guid}/deliveries")]
    public async Task<IActionResult> GetDeliveries(Guid id, CancellationToken cancellationToken)
    {
        var deliveries = await _webhookService.GetDeliveriesAsync(id, cancellationToken);
        return Ok(deliveries);
    }

    [HttpPost("{id:guid}/test")]
    public async Task<IActionResult> Test(Guid id, CancellationToken cancellationToken)
    {
        var result = await _webhookService.TestWebhookAsync(id, cancellationToken);
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
