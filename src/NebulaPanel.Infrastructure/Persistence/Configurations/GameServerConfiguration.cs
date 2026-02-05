using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.Persistence.Configurations;

public class GameServerConfiguration : IEntityTypeConfiguration<GameServer>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public void Configure(EntityTypeBuilder<GameServer> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .ValueGeneratedNever();

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(s => s.Description)
            .HasMaxLength(1000);

        builder.Property(s => s.IconPath)
            .HasMaxLength(500);

        builder.Property(s => s.DeploymentType)
            .IsRequired();

        builder.Property(s => s.InstallPath)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(s => s.DockerContainerId)
            .HasMaxLength(100);

        builder.Property(s => s.IsPinned)
            .HasDefaultValue(false);

        builder.Property(s => s.Tags)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonOptions) ?? new List<string>())
            .HasColumnType("TEXT")
            .HasDefaultValue(new List<string>())
            .Metadata.SetValueComparer(new ValueComparer<List<string>>(
                (c1, c2) => JsonSerializer.Serialize(c1, JsonOptions) == JsonSerializer.Serialize(c2, JsonOptions),
                c => c == null ? 0 : JsonSerializer.Serialize(c, JsonOptions).GetHashCode(),
                c => JsonSerializer.Deserialize<List<string>>(JsonSerializer.Serialize(c, JsonOptions), JsonOptions)!));

        builder.Property(s => s.BindAddress)
            .IsRequired()
            .HasMaxLength(45)
            .HasDefaultValue("0.0.0.0");

        // JSON columns
        builder.Property(s => s.DockerConfig)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, JsonOptions),
                v => v == null ? null : JsonSerializer.Deserialize<DockerConfiguration>(v, JsonOptions))
            .HasColumnType("TEXT");

        builder.Property(s => s.NativeConfig)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, JsonOptions),
                v => v == null ? null : JsonSerializer.Deserialize<NativeConfiguration>(v, JsonOptions))
            .HasColumnType("TEXT");

        builder.Property(s => s.RconConfig)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, JsonOptions),
                v => v == null ? null : JsonSerializer.Deserialize<RconConfiguration>(v, JsonOptions))
            .HasColumnType("TEXT");

        builder.Property(s => s.AdditionalPorts)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Dictionary<string, int>>(v, JsonOptions) ?? new Dictionary<string, int>())
            .HasColumnType("TEXT")
            .Metadata.SetValueComparer(new ValueComparer<Dictionary<string, int>>(
                (c1, c2) => JsonSerializer.Serialize(c1, JsonOptions) == JsonSerializer.Serialize(c2, JsonOptions),
                c => c == null ? 0 : JsonSerializer.Serialize(c, JsonOptions).GetHashCode(),
                c => JsonSerializer.Deserialize<Dictionary<string, int>>(JsonSerializer.Serialize(c, JsonOptions), JsonOptions)!));

        builder.Property(s => s.ResourceLimits)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, JsonOptions),
                v => v == null ? null : JsonSerializer.Deserialize<ResourceLimits>(v, JsonOptions))
            .HasColumnType("TEXT");

        builder.Property(s => s.InstalledModpack)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, JsonOptions),
                v => v == null ? null : JsonSerializer.Deserialize<InstalledModpackInfo>(v, JsonOptions))
            .HasColumnType("TEXT");

        builder.Property(s => s.HytaleInfo)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, JsonOptions),
                v => v == null ? null : JsonSerializer.Deserialize<HytaleServerInfo>(v, JsonOptions))
            .HasColumnType("TEXT");

        // Concurrency token for optimistic locking
        // Note: SQLite doesn't support auto-generated row versions like SQL Server
        // Using IsConcurrencyToken() instead of IsRowVersion() for cross-database compatibility
        builder.Property(s => s.RowVersion)
            .IsConcurrencyToken()
            .IsRequired()
            .HasDefaultValueSql("randomblob(8)");

        // Indexes per specification
        builder.HasIndex(s => s.GameId);
        builder.HasIndex(s => s.OwnerId);
        builder.HasIndex(s => s.DeploymentType);

        // Unique constraint: owner_id + name
        builder.HasIndex(s => new { s.OwnerId, s.Name })
            .IsUnique();

        // Relationships
        builder.HasOne(s => s.Game)
            .WithMany(g => g.Servers)
            .HasForeignKey(s => s.GameId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.Owner)
            .WithMany(u => u.OwnedServers)
            .HasForeignKey(s => s.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.InstalledMods)
            .WithOne(m => m.Server)
            .HasForeignKey(m => m.ServerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.ScheduledTasks)
            .WithOne(t => t.Server)
            .HasForeignKey(t => t.ServerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.Backups)
            .WithOne(b => b.Server)
            .HasForeignKey(b => b.ServerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ignore computed property
        builder.Ignore(s => s.PreferredCommandMethod);
    }
}
