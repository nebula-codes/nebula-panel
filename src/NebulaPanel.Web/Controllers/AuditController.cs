using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[IgnoreAntiforgeryToken]
[Authorize(Roles = "Admin")]
public class AuditController(IAuditQueryService auditQueryService) : ControllerBase
{
    private readonly IAuditQueryService _auditQueryService = auditQueryService;

    [HttpGet]
    public async Task<IActionResult> Query(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] Guid? userId = null,
        [FromQuery] SecurityEventType? eventType = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] string? searchTerm = null,
        CancellationToken cancellationToken = default)
    {
        var request = new AuditLogQueryRequest(
            page, pageSize, userId, eventType, from, to, searchTerm);

        var result = await _auditQueryService.QueryAsync(request, cancellationToken);
        return Ok(result);
    }
}
