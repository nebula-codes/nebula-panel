using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.Persistence.Configurations;

public class NodeConfiguration : IEntityTypeConfiguration<Node>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public void Configure(EntityTypeBuilder<Node> builder)
    {
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Id)
            .ValueGeneratedNever();

        builder.Property(n => n.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(n => n.Address)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(n => n.AgentToken)
            .HasMaxLength(500);

        builder.Property(n => n.Status)
            .IsRequired();

        builder.Property(n => n.LastHeartbeat)
            .IsRequired();

        builder.Property(n => n.Capacity)
            .HasConversion(
                v => v == null ? null : JsonSerializer.Serialize(v, JsonOptions),
                v => v == null ? null : JsonSerializer.Deserialize<ResourceLimits>(v, JsonOptions))
            .HasColumnType("TEXT");

        builder.HasIndex(n => n.Name)
            .IsUnique();

        builder.HasIndex(n => n.Status);
    }
}
