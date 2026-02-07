using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Infrastructure.Persistence.Configurations;

public class SystemSettingsConfiguration : IEntityTypeConfiguration<SystemSettings>
{
    public void Configure(EntityTypeBuilder<SystemSettings> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .ValueGeneratedNever();

        builder.Property(s => s.GeneralSettingsJson)
            .IsRequired()
            .HasDefaultValue("{}");

        builder.Property(s => s.UpdateSettingsJson)
            .IsRequired()
            .HasDefaultValue("{}");

        builder.Property(s => s.AppearanceSettingsJson)
            .IsRequired()
            .HasDefaultValue("{}");

        builder.Property(s => s.ModCacheSettingsJson)
            .IsRequired()
            .HasDefaultValue("{}");

        builder.Property(s => s.IntegrationSettingsJson)
            .IsRequired()
            .HasDefaultValue("{}");

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.LastModifiedAt)
            .IsRequired();

        builder.HasOne(s => s.LastModifiedByUser)
            .WithMany()
            .HasForeignKey(s => s.LastModifiedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
