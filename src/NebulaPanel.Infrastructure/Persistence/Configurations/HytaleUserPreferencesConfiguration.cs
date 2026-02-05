using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Infrastructure.Persistence.Configurations;

public class HytaleUserPreferencesConfiguration : IEntityTypeConfiguration<HytaleUserPreferences>
{
    public void Configure(EntityTypeBuilder<HytaleUserPreferences> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedNever();

        builder.Property(p => p.AutoRefreshToken)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(p => p.NotifyAt24Hours)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(p => p.NotifyAt1Hour)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(p => p.EmailOnExpiry)
            .IsRequired()
            .HasDefaultValue(false);

        // Unique index on UserId - one preferences record per user
        builder.HasIndex(p => p.UserId)
            .IsUnique();

        // Relationship with User
        builder.HasOne(p => p.User)
            .WithOne(u => u.HytalePreferences)
            .HasForeignKey<HytaleUserPreferences>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
