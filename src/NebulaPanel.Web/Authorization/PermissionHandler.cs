namespace NebulaPanel.Web.Authorization;

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using NebulaPanel.Application.Services;

public class PermissionHandler(IPermissionService permissionService) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)
            ?? context.User.FindFirst("sub");

        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            return;
        }

        if (await permissionService.HasPermissionAsync(userId, requirement.Permission).ConfigureAwait(false))
        {
            context.Succeed(requirement);
        }
    }
}
