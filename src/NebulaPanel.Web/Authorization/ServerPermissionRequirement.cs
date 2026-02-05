namespace NebulaPanel.Web.Authorization;

using Microsoft.AspNetCore.Authorization;

public class ServerPermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
