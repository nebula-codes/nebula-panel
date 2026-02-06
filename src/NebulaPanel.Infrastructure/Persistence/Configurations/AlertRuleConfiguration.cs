using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Infrastructure.Persistence.Configurations;

public class AlertRuleConfiguration : IEntityTypeConfiguration<AlertRule>
{
    public void Configure(EntityTypeBuilder<AlertRule> builder)
    {
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(r => r.ResourceType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(r => r.Comparison)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(r => r.Threshold).IsRequired();
        builder.Property(r => r.DurationSeconds).IsRequired();
        builder.Property(r => r.IsEnabled).IsRequired();
        builder.Property(r => r.CooldownMinutes).IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();

        builder.HasOne(r => r.Server)
            .WithMany()
            .HasForeignKey(r => r.ServerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Owner)
            .WithMany()
            .HasForeignKey(r => r.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.OwnerId);
        builder.HasIndex(r => r.ServerId);
        builder.HasIndex(r => r.IsEnabled);
    }
}
