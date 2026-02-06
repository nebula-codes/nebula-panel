using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Infrastructure.Persistence.Configurations;

public class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.EventType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(d => d.Payload).IsRequired();
        builder.Property(d => d.Success).IsRequired();
        builder.Property(d => d.AttemptedAt).IsRequired();
        builder.Property(d => d.DurationMs).IsRequired();
        builder.Property(d => d.AttemptNumber).IsRequired();

        builder.HasIndex(d => d.EndpointId);
        builder.HasIndex(d => d.AttemptedAt);
    }
}
