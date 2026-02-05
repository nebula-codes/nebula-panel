namespace NebulaPanel.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Constants;
using NebulaPanel.Domain.Entities;

public static class DataSeeder
{
    public static async Task SeedDataAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<NebulaPanelDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<NebulaPanelDbContext>>();

        await SeedPermissionsAsync(context, logger);
        await SeedRolesAsync(context, logger);
        await SeedDefaultAdminUserAsync(context, logger);
    }

    private static async Task SeedDefaultAdminUserAsync(NebulaPanelDbContext context, ILogger logger)
    {
        // Check if any users exist
        var anyUsers = await context.Users.AnyAsync();
        if (anyUsers)
            return;

        var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
        if (adminRole == null)
        {
            logger.LogWarning("Admin role not found, cannot create default admin user");
            return;
        }

        // Create default admin user
        const string defaultPassword = "admin";
        var adminUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "admin",
            Email = "admin@localhost",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(defaultPassword),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(adminUser);
        await context.SaveChangesAsync();

        // Assign admin role
        context.UserRoles.Add(new UserRole
        {
            UserId = adminUser.Id,
            RoleId = adminRole.Id,
            AssignedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        logger.LogWarning("========================================");
        logger.LogWarning("DEFAULT ADMIN USER CREATED");
        logger.LogWarning("Username: admin");
        logger.LogWarning("Password: admin");
        logger.LogWarning("PLEASE CHANGE THE PASSWORD IMMEDIATELY!");
        logger.LogWarning("========================================");
    }

    private static async Task SeedPermissionsAsync(NebulaPanelDbContext context, ILogger logger)
    {
        var existingPermissions = await context.Permissions.Select(p => p.Code).ToListAsync();

        var permissions = new List<Permission>
        {
            // System permissions
            new() { Id = Guid.NewGuid(), Code = Permissions.System.ViewDashboard, Name = "View Dashboard", Category = "System", Description = "View the main dashboard" },
            new() { Id = Guid.NewGuid(), Code = Permissions.System.ViewHostMetrics, Name = "View Host Metrics", Category = "System", Description = "View host system metrics" },
            new() { Id = Guid.NewGuid(), Code = Permissions.System.ManageSettings, Name = "Manage Settings", Category = "System", Description = "Manage system settings" },
            new() { Id = Guid.NewGuid(), Code = Permissions.System.ViewLogs, Name = "View Logs", Category = "System", Description = "View system logs" },

            // User permissions
            new() { Id = Guid.NewGuid(), Code = Permissions.Users.View, Name = "View Users", Category = "Users", Description = "View user list" },
            new() { Id = Guid.NewGuid(), Code = Permissions.Users.Create, Name = "Create Users", Category = "Users", Description = "Create new users" },
            new() { Id = Guid.NewGuid(), Code = Permissions.Users.Edit, Name = "Edit Users", Category = "Users", Description = "Edit existing users" },
            new() { Id = Guid.NewGuid(), Code = Permissions.Users.Delete, Name = "Delete Users", Category = "Users", Description = "Delete users" },
            new() { Id = Guid.NewGuid(), Code = Permissions.Users.ManageRoles, Name = "Manage Roles", Category = "Users", Description = "Manage user roles" },

            // Game permissions
            new() { Id = Guid.NewGuid(), Code = Permissions.Games.View, Name = "View Games", Category = "Games", Description = "View game definitions" },
            new() { Id = Guid.NewGuid(), Code = Permissions.Games.Create, Name = "Create Games", Category = "Games", Description = "Create new game definitions" },
            new() { Id = Guid.NewGuid(), Code = Permissions.Games.Edit, Name = "Edit Games", Category = "Games", Description = "Edit game definitions" },
            new() { Id = Guid.NewGuid(), Code = Permissions.Games.Delete, Name = "Delete Games", Category = "Games", Description = "Delete game definitions" },

            // Server permissions (global)
            new() { Id = Guid.NewGuid(), Code = Permissions.Servers.ViewOwn, Name = "View Own Servers", Category = "Servers", Description = "View own servers" },
            new() { Id = Guid.NewGuid(), Code = Permissions.Servers.ViewAll, Name = "View All Servers", Category = "Servers", Description = "View all servers" },
            new() { Id = Guid.NewGuid(), Code = Permissions.Servers.Create, Name = "Create Servers", Category = "Servers", Description = "Create new servers" },
            new() { Id = Guid.NewGuid(), Code = Permissions.Servers.Delete, Name = "Delete Servers", Category = "Servers", Description = "Delete servers" },

            // Server permissions (per-server, these are templates)
            new() { Id = Guid.NewGuid(), Code = Permissions.Servers.Start, Name = "Start Server", Category = "Servers", Description = "Start a server" },
            new() { Id = Guid.NewGuid(), Code = Permissions.Servers.Stop, Name = "Stop Server", Category = "Servers", Description = "Stop a server" },
            new() { Id = Guid.NewGuid(), Code = Permissions.Servers.Restart, Name = "Restart Server", Category = "Servers", Description = "Restart a server" },
            new() { Id = Guid.NewGuid(), Code = Permissions.Servers.Console, Name = "Server Console", Category = "Servers", Description = "Access server console" },
            new() { Id = Guid.NewGuid(), Code = Permissions.Servers.Files, Name = "Server Files", Category = "Servers", Description = "Manage server files" },
            new() { Id = Guid.NewGuid(), Code = Permissions.Servers.Config, Name = "Server Config", Category = "Servers", Description = "Edit server configuration" },
            new() { Id = Guid.NewGuid(), Code = Permissions.Servers.Mods, Name = "Server Mods", Category = "Servers", Description = "Manage server mods" },
            new() { Id = Guid.NewGuid(), Code = Permissions.Servers.Backup, Name = "Server Backups", Category = "Servers", Description = "Manage server backups" },
            new() { Id = Guid.NewGuid(), Code = Permissions.Servers.Schedule, Name = "Server Schedule", Category = "Servers", Description = "Manage scheduled tasks" },
        };

        var newPermissions = permissions.Where(p => !existingPermissions.Contains(p.Code)).ToList();

        if (newPermissions.Count > 0)
        {
            await context.Permissions.AddRangeAsync(newPermissions);
            await context.SaveChangesAsync();
            logger.LogInformation("Seeded {Count} new permissions", newPermissions.Count);
        }
    }

    private static async Task SeedRolesAsync(NebulaPanelDbContext context, ILogger logger)
    {
        var existingRoles = await context.Roles
            .Include(r => r.Permissions)
            .ToListAsync();

        var allPermissions = await context.Permissions.ToListAsync();

        // Admin role - has all permissions
        if (!existingRoles.Any(r => r.Name == "Admin"))
        {
            var adminRole = new Role
            {
                Id = Guid.NewGuid(),
                Name = "Admin",
                Description = "Full system administrator with all permissions",
                IsSystemRole = true,
                Priority = 100
            };

            context.Roles.Add(adminRole);
            await context.SaveChangesAsync();

            // Add all permissions to admin role
            foreach (var permission in allPermissions)
            {
                context.RolePermissions.Add(new RolePermission
                {
                    RoleId = adminRole.Id,
                    PermissionId = permission.Id
                });
            }

            await context.SaveChangesAsync();
            logger.LogInformation("Seeded Admin role with all permissions");
        }

        // Moderator role - can manage servers but not users/system
        if (!existingRoles.Any(r => r.Name == "Moderator"))
        {
            var modRole = new Role
            {
                Id = Guid.NewGuid(),
                Name = "Moderator",
                Description = "Can manage all servers but not users or system settings",
                IsSystemRole = true,
                Priority = 50
            };

            context.Roles.Add(modRole);
            await context.SaveChangesAsync();

            var modPermissions = allPermissions.Where(p =>
                p.Code.StartsWith("system.dashboard") ||
                p.Code.StartsWith("system.metrics") ||
                p.Code.StartsWith("games.view") ||
                p.Code.StartsWith("servers.")).ToList();

            foreach (var permission in modPermissions)
            {
                context.RolePermissions.Add(new RolePermission
                {
                    RoleId = modRole.Id,
                    PermissionId = permission.Id
                });
            }

            await context.SaveChangesAsync();
            logger.LogInformation("Seeded Moderator role");
        }

        // User role - can create and manage own servers
        if (!existingRoles.Any(r => r.Name == "User"))
        {
            var userRole = new Role
            {
                Id = Guid.NewGuid(),
                Name = "User",
                Description = "Standard user who can create and manage their own servers",
                IsSystemRole = true,
                Priority = 10
            };

            context.Roles.Add(userRole);
            await context.SaveChangesAsync();

            var userPermissions = allPermissions.Where(p =>
                p.Code == Permissions.System.ViewDashboard ||
                p.Code == Permissions.Games.View ||
                p.Code == Permissions.Servers.ViewOwn ||
                p.Code == Permissions.Servers.Create ||
                p.Code.StartsWith("servers.{id}")).ToList();

            foreach (var permission in userPermissions)
            {
                context.RolePermissions.Add(new RolePermission
                {
                    RoleId = userRole.Id,
                    PermissionId = permission.Id
                });
            }

            await context.SaveChangesAsync();
            logger.LogInformation("Seeded User role");
        }
    }
}
