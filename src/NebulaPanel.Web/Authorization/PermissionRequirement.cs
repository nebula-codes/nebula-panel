namespace NebulaPanel.Web.Authorization;

using Microsoft.AspNetCore.Authorization;

public class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
