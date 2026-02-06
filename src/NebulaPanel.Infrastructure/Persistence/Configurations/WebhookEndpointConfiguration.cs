using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Infrastructure.Persistence.Configurations;

public class WebhookEndpointConfiguration : IEntityTypeConfiguration<WebhookEndpoint>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public void Configure(EntityTypeBuilder<WebhookEndpoint> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Url).IsRequired().HasMaxLength(2048);
        builder.Property(e => e.Secret).IsRequired().HasMaxLength(128);
        builder.Property(e => e.IsEnabled).IsRequired();
        builder.Property(e => e.FailureCount).IsRequired();
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.Property(e => e.SubscribedEvents)
            .HasConversion(
                v => JsonSerializer.Serialize(v, JsonOptions),
                v => JsonSerializer.Deserialize<List<WebhookEventType>>(v, JsonOptions) ?? new List<WebhookEventType>())
            .HasColumnType("TEXT")
            .Metadata.SetValueComparer(new ValueComparer<List<WebhookEventType>>(
                (a, b) => (a ?? new()).SequenceEqual(b ?? new()),
                c => c.Aggregate(0, (hash, item) => HashCode.Combine(hash, (int)item)),
                c => c.ToList()));

        builder.HasOne(e => e.Owner)
            .WithMany()
            .HasForeignKey(e => e.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Deliveries)
            .WithOne(d => d.Endpoint)
            .HasForeignKey(d => d.EndpointId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.OwnerId);
        builder.HasIndex(e => e.IsEnabled);
    }
}
