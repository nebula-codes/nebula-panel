using Microsoft.EntityFrameworkCore;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Infrastructure.Persistence.Conventions;

namespace NebulaPanel.Infrastructure.Persistence;

public class NebulaPanelDbContext(DbContextOptions<NebulaPanelDbContext> options)
    : DbContext(options)
{
    // Main entities
    public DbSet<Game> Games => Set<Game>();
    public DbSet<GameServer> GameServers => Set<GameServer>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();

    // Join tables
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    // Auth entities
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<HytaleUserCredentials> HytaleUserCredentials => Set<HytaleUserCredentials>();
    public DbSet<HytaleUserPreferences> HytaleUserPreferences => Set<HytaleUserPreferences>();

    // Server-related entities
    public DbSet<ServerPermission> ServerPermissions => Set<ServerPermission>();
    public DbSet<ServerMod> ServerMods => Set<ServerMod>();
    public DbSet<ScheduledTask> ScheduledTasks => Set<ScheduledTask>();
    public DbSet<Backup> Backups => Set<Backup>();

    // Activity logging
    public DbSet<UserActivity> UserActivities => Set<UserActivity>();
    public DbSet<ServerActivity> ServerActivities => Set<ServerActivity>();

    // Dashboard
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<ResourceUsageHistory> ResourceUsageHistory => Set<ResourceUsageHistory>();

    // System settings
    public DbSet<SystemSettings> SystemSettings => Set<SystemSettings>();

    // Mod cache
    public DbSet<CachedMod> CachedMods => Set<CachedMod>();
    public DbSet<ModCacheSyncStatus> ModCacheSyncStatuses => Set<ModCacheSyncStatus>();

    // Update system
    public DbSet<UpdateSchedule> UpdateSchedules => Set<UpdateSchedule>();
    public DbSet<UpdateHistory> UpdateHistories => Set<UpdateHistory>();

    // Notifications
    public DbSet<Notification> Notifications => Set<Notification>();

    // Security audit
    public DbSet<SecurityAuditEvent> SecurityAuditEvents => Set<SecurityAuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NebulaPanelDbContext).Assembly);

        // Apply snake_case naming convention last
        modelBuilder.ApplySnakeCaseNaming();
    }
}
