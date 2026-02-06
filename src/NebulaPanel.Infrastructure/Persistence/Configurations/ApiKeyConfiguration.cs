using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Infrastructure.Persistence.Configurations;

public class ApiKeyConfiguration : IEntityTypeConfiguration<ApiKey>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public void Configure(EntityTypeBuilder<ApiKey> builder)
    {
        builder.HasKey(k => k.Id);
        builder.Property(k => k.Id).ValueGeneratedNever();

        builder.Property(k => k.Name).IsRequired().HasMaxLength(200);
        builder.Property(k => k.KeyHash).IsRequired().HasMaxLength(128);
        builder.Property(k => k.KeyPrefix).IsRequired().HasMaxLength(20);
        builder.Property(k => k.CreatedAt).IsRequired();

        builder.Property(k => k.Scopes)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<string>>(v, JsonOptions) ?? new List<string>())
            .HasColumnType("TEXT");

        builder.HasOne(k => k.User)
            .WithMany()
            .HasForeignKey(k => k.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(k => k.UserId);
        builder.HasIndex(k => k.KeyHash).IsUnique();
    }
}
