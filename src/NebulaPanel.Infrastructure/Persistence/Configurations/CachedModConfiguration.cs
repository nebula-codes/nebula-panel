using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Infrastructure.Persistence.Configurations;

public class CachedModConfiguration : IEntityTypeConfiguration<CachedMod>
{
    public void Configure(EntityTypeBuilder<CachedMod> builder)
    {
        builder.HasKey(m => m.Id);

        builder.HasIndex(m => new { m.Provider, m.ProviderModId })
            .IsUnique()
            .HasDatabaseName("ix_cached_mods_provider_provider_mod_id");

        builder.HasIndex(m => m.Name)
            .HasDatabaseName("ix_cached_mods_name");

        builder.HasIndex(m => new { m.Provider, m.ContentType })
            .HasDatabaseName("ix_cached_mods_provider_content_type");

        builder.HasIndex(m => m.Downloads)
            .HasDatabaseName("ix_cached_mods_downloads");

        builder.HasIndex(m => m.UpdatedAt)
            .HasDatabaseName("ix_cached_mods_updated_at");

        builder.HasIndex(m => m.CachedAt)
            .HasDatabaseName("ix_cached_mods_cached_at");

        builder.Property(m => m.Provider)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(m => m.ProviderModId)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(m => m.Slug)
            .IsRequired()
            .HasMaxLength(256);

        builder.Property(m => m.Name)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(m => m.Summary)
            .HasMaxLength(2000);

        builder.Property(m => m.IconUrl)
            .HasMaxLength(1024);

        builder.Property(m => m.Author)
            .HasMaxLength(256);

        builder.Property(m => m.ContentType)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(m => m.CategoriesJson)
            .IsRequired()
            .HasDefaultValue("[]");

        builder.Property(m => m.GameVersionsJson)
            .IsRequired()
            .HasDefaultValue("[]");

        builder.Property(m => m.LoadersJson)
            .IsRequired()
            .HasDefaultValue("[]");

        builder.Property(m => m.CachedAt)
            .IsRequired();

        builder.Property(m => m.UpdatedAt)
            .IsRequired();
    }
}
