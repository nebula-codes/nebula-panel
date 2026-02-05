namespace NebulaPanel.Web.Authorization;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Constants;

public class ServerPermissionHandler(
    IPermissionService permissionService,
    IHttpContextAccessor httpContextAccessor) : AuthorizationHandler<ServerPermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ServerPermissionRequirement requirement)
    {
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirst("sub");

        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return;
        }

        // Try to get server ID from route values
        var httpContext = httpContextAccessor.HttpContext;
        Guid? serverId = null;

        if (httpContext?.Request.RouteValues.TryGetValue("serverId", out var serverIdObj) == true)
        {
            if (serverIdObj is string serverIdString && Guid.TryParse(serverIdString, out var parsedId))
            {
                serverId = parsedId;
            }
            else if (serverIdObj is Guid guidId)
            {
                serverId = guidId;
            }
        }

        // Also check the resource if provided (for when authorization is called with a specific resource)
        if (!serverId.HasValue && context.Resource is Guid resourceGuid)
        {
            serverId = resourceGuid;
        }

        if (!serverId.HasValue)
        {
            // No server ID found, cannot authorize
            return;
        }

        // Build the full permission code
        var permissionCode = Permissions.Servers.ForServer(requirement.Permission, serverId.Value);

        if (await permissionService.HasServerPermissionAsync(userId, serverId.Value, permissionCode).ConfigureAwait(false))
        {
            context.Succeed(requirement);
        }
    }
}
