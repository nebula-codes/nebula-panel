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
public class NodesController(INodeService nodeService) : ControllerBase
{
    private readonly INodeService _nodeService = nodeService;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NodeListItemDto>>> GetAll(CancellationToken cancellationToken)
    {
        var nodes = await _nodeService.GetAllNodesAsync(cancellationToken);
        return Ok(nodes);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _nodeService.GetNodeByIdAsync(id, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateNodeRequest request, CancellationToken cancellationToken)
    {
        var result = await _nodeService.CreateNodeAsync(request, cancellationToken);
        return result.ToCreatedResult();
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateNodeRequest request, CancellationToken cancellationToken)
    {
        var result = await _nodeService.UpdateNodeAsync(id, request, cancellationToken);
        return result.ToActionResult();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _nodeService.DeleteNodeAsync(id, cancellationToken);
        return result.ToNoContentResult();
    }

    [HttpPost("{id:guid}/regenerate-token")]
    public async Task<IActionResult> RegenerateToken(Guid id, CancellationToken cancellationToken)
    {
        var result = await _nodeService.RegenerateTokenAsync(id, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/drain")]
    public async Task<IActionResult> Drain(Guid id, CancellationToken cancellationToken)
    {
        var result = await _nodeService.DrainNodeAsync(id, cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("{id:guid}/undrain")]
    public async Task<IActionResult> Undrain(Guid id, CancellationToken cancellationToken)
    {
        var result = await _nodeService.UndrainNodeAsync(id, cancellationToken);
        return result.ToActionResult();
    }
}
