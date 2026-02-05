namespace NebulaPanel.Web.Authorization;

using Microsoft.AspNetCore.Authorization;
using NebulaPanel.Domain.Constants;

public static class AuthorizationExtensions
{
    public static AuthorizationOptions AddNebulaPolicies(this AuthorizationOptions options)
    {
        // System policies
        options.AddPolicy("ViewDashboard", policy =>
            policy.Requirements.Add(new PermissionRequirement(Permissions.System.ViewDashboard)));
        options.AddPolicy("ViewHostMetrics", policy =>
            policy.Requirements.Add(new PermissionRequirement(Permissions.System.ViewHostMetrics)));
        options.AddPolicy("ManageSettings", policy =>
            policy.Requirements.Add(new PermissionRequirement(Permissions.System.ManageSettings)));
        options.AddPolicy("ViewLogs", policy =>
            policy.Requirements.Add(new PermissionRequirement(Permissions.System.ViewLogs)));

        // User management policies
        options.AddPolicy("ViewUsers", policy =>
            policy.Requirements.Add(new PermissionRequirement(Permissions.Users.View)));
        options.AddPolicy("CreateUsers", policy =>
            policy.Requirements.Add(new PermissionRequirement(Permissions.Users.Create)));
        options.AddPolicy("EditUsers", policy =>
            policy.Requirements.Add(new PermissionRequirement(Permissions.Users.Edit)));
        options.AddPolicy("DeleteUsers", policy =>
            policy.Requirements.Add(new PermissionRequirement(Permissions.Users.Delete)));
        options.AddPolicy("ManageRoles", policy =>
            policy.Requirements.Add(new PermissionRequirement(Permissions.Users.ManageRoles)));

        // Game management policies
        options.AddPolicy("ViewGames", policy =>
            policy.Requirements.Add(new PermissionRequirement(Permissions.Games.View)));
        options.AddPolicy("CreateGames", policy =>
            policy.Requirements.Add(new PermissionRequirement(Permissions.Games.Create)));
        options.AddPolicy("EditGames", policy =>
            policy.Requirements.Add(new PermissionRequirement(Permissions.Games.Edit)));
        options.AddPolicy("DeleteGames", policy =>
            policy.Requirements.Add(new PermissionRequirement(Permissions.Games.Delete)));

        // Server management policies (global)
        options.AddPolicy("ViewOwnServers", policy =>
            policy.Requirements.Add(new PermissionRequirement(Permissions.Servers.ViewOwn)));
        options.AddPolicy("ViewAllServers", policy =>
            policy.Requirements.Add(new PermissionRequirement(Permissions.Servers.ViewAll)));
        options.AddPolicy("CreateServers", policy =>
            policy.Requirements.Add(new PermissionRequirement(Permissions.Servers.Create)));
        options.AddPolicy("DeleteServers", policy =>
            policy.Requirements.Add(new PermissionRequirement(Permissions.Servers.Delete)));

        // Per-server action policies
        options.AddPolicy("ServerStart", policy =>
            policy.Requirements.Add(new ServerPermissionRequirement(Permissions.Servers.Start)));
        options.AddPolicy("ServerStop", policy =>
            policy.Requirements.Add(new ServerPermissionRequirement(Permissions.Servers.Stop)));
        options.AddPolicy("ServerRestart", policy =>
            policy.Requirements.Add(new ServerPermissionRequirement(Permissions.Servers.Restart)));
        options.AddPolicy("ServerConsole", policy =>
            policy.Requirements.Add(new ServerPermissionRequirement(Permissions.Servers.Console)));
        options.AddPolicy("ServerFiles", policy =>
            policy.Requirements.Add(new ServerPermissionRequirement(Permissions.Servers.Files)));
        options.AddPolicy("ServerConfig", policy =>
            policy.Requirements.Add(new ServerPermissionRequirement(Permissions.Servers.Config)));
        options.AddPolicy("ServerMods", policy =>
            policy.Requirements.Add(new ServerPermissionRequirement(Permissions.Servers.Mods)));
        options.AddPolicy("ServerBackup", policy =>
            policy.Requirements.Add(new ServerPermissionRequirement(Permissions.Servers.Backup)));
        options.AddPolicy("ServerSchedule", policy =>
            policy.Requirements.Add(new ServerPermissionRequirement(Permissions.Servers.Schedule)));

        // Admin policy
        options.AddPolicy("Admin", policy =>
            policy.RequireRole("Admin"));

        return options;
    }
}
