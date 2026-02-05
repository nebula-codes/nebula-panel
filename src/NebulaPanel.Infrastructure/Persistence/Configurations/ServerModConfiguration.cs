using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Infrastructure.Persistence.Configurations;

public class ServerModConfiguration : IEntityTypeConfiguration<ServerMod>
{
    public void Configure(EntityTypeBuilder<ServerMod> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id)
            .ValueGeneratedNever();

        builder.Property(m => m.ModId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(m => m.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(m => m.Version)
            .HasMaxLength(50);

        builder.Property(m => m.VersionId)
            .HasMaxLength(100);

        builder.Property(m => m.Provider)
            .IsRequired();

        builder.Property(m => m.InstalledAt)
            .IsRequired();

        builder.Property(m => m.IsEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(m => m.LocalPath)
            .HasMaxLength(1000);

        builder.Property(m => m.IconUrl)
            .HasMaxLength(500);

        builder.Property(m => m.FileHash)
            .HasMaxLength(128);

        // Index on server_id per specification
        builder.HasIndex(m => m.ServerId);

        // Unique constraint: server_id + provider + mod_id per specification
        builder.HasIndex(m => new { m.ServerId, m.Provider, m.ModId })
            .IsUnique();

        // Relationship with cascade delete
        builder.HasOne(m => m.Server)
            .WithMany(s => s.InstalledMods)
            .HasForeignKey(m => m.ServerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
