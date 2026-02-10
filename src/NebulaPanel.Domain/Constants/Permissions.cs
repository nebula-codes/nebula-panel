namespace NebulaPanel.Domain.Constants;

/// <summary>
/// Permission code constants for authorization.
/// </summary>
public static class Permissions
{
    public static class System
    {
        public const string ViewDashboard = "system.dashboard.view";
        public const string ViewHostMetrics = "system.metrics.view";
        public const string ManageSettings = "system.settings.manage";
        public const string ViewLogs = "system.logs.view";
    }

    public static class Users
    {
        public const string View = "users.view";
        public const string Create = "users.create";
        public const string Edit = "users.edit";
        public const string Delete = "users.delete";
        public const string ManageRoles = "users.roles.manage";
    }

    public static class Games
    {
        public const string View = "games.view";
        public const string Create = "games.create";
        public const string Edit = "games.edit";
        public const string Delete = "games.delete";
    }

    public static class Nodes
    {
        public const string View = "nodes.view";
        public const string Create = "nodes.create";
        public const string Edit = "nodes.edit";
        public const string Delete = "nodes.delete";
    }

    public static class Servers
    {
        public const string ViewOwn = "servers.own.view";
        public const string ViewAll = "servers.all.view";
        public const string Create = "servers.create";
        public const string Delete = "servers.delete";

        // Per-server actions (can be overridden per-server)
        public const string Start = "servers.{id}.start";
        public const string Stop = "servers.{id}.stop";
        public const string Restart = "servers.{id}.restart";
        public const string Console = "servers.{id}.console";
        public const string Files = "servers.{id}.files";
        public const string Config = "servers.{id}.config";
        public const string Mods = "servers.{id}.mods";
        public const string Backup = "servers.{id}.backup";
        public const string Schedule = "servers.{id}.schedule";

        /// <summary>
        /// Generates a server-specific permission code by replacing {id} with the actual server ID.
        /// </summary>
        public static string ForServer(string permissionTemplate, Guid serverId)
            => permissionTemplate.Replace("{id}", serverId.ToString());
    }
}
