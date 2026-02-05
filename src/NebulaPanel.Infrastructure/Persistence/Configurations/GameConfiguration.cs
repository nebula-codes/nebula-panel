using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.Persistence.Configurations;

public class GameConfiguration : IEntityTypeConfiguration<Game>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public void Configure(EntityTypeBuilder<Game> builder)
    {
        builder.HasKey(g => g.Id);

        builder.Property(g => g.Id)
            .ValueGeneratedNever();

        builder.Property(g => g.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(g => g.Slug)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(g => g.Slug)
            .IsUnique();

        builder.Property(g => g.SteamAppId)
            .HasMaxLength(20);

        builder.Property(g => g.ExecutableType)
            .IsRequired();

        builder.Property(g => g.DefaultExecutablePath)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(g => g.DefaultStartCommand)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(g => g.DefaultStopCommand)
            .HasMaxLength(2000);

        builder.Property(g => g.DefaultDockerImage)
            .HasMaxLength(255);

        builder.Property(g => g.IconPath)
            .HasMaxLength(500);

        // JSON column: ModProviders (List<ModProviderConfiguration>)
        builder.Property(g => g.ModProviders)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<ModProviderConfiguration>>(v, JsonOptions) ?? new List<ModProviderConfiguration>())
            .HasColumnType("TEXT")
            .Metadata.SetValueComparer(new ValueComparer<List<ModProviderConfiguration>>(
                (c1, c2) => JsonSerializer.Serialize(c1, JsonOptions) == JsonSerializer.Serialize(c2, JsonOptions),
                c => c == null ? 0 : JsonSerializer.Serialize(c, JsonOptions).GetHashCode(),
                c => JsonSerializer.Deserialize<List<ModProviderConfiguration>>(JsonSerializer.Serialize(c, JsonOptions), JsonOptions)!));

        // JSON column: RconDefaults (nullable)
        builder.Property(g => g.RconDefaults)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, JsonOptions),
                v => v == null ? null : JsonSerializer.Deserialize<RconDefaults>(v, JsonOptions))
            .HasColumnType("TEXT");

        // JSON column: ConfigurationSchemas (Dictionary)
        builder.Property(g => g.ConfigurationSchemas)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<Dictionary<string, ConfigurationSchema>>(v, JsonOptions) ?? new Dictionary<string, ConfigurationSchema>())
            .HasColumnType("TEXT")
            .Metadata.SetValueComparer(new ValueComparer<Dictionary<string, ConfigurationSchema>>(
                (c1, c2) => JsonSerializer.Serialize(c1, JsonOptions) == JsonSerializer.Serialize(c2, JsonOptions),
                c => c == null ? 0 : JsonSerializer.Serialize(c, JsonOptions).GetHashCode(),
                c => JsonSerializer.Deserialize<Dictionary<string, ConfigurationSchema>>(JsonSerializer.Serialize(c, JsonOptions), JsonOptions)!));

        // Navigation
        builder.HasMany(g => g.Servers)
            .WithOne(s => s.Game)
            .HasForeignKey(s => s.GameId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
