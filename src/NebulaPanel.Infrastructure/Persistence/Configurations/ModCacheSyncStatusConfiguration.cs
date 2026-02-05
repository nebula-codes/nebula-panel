using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Infrastructure.Persistence.Configurations;

public class ModCacheSyncStatusConfiguration : IEntityTypeConfiguration<ModCacheSyncStatus>
{
    public void Configure(EntityTypeBuilder<ModCacheSyncStatus> builder)
    {
        builder.HasKey(s => s.Id);

        builder.HasIndex(s => new { s.Provider, s.ContentType })
            .IsUnique()
            .HasDatabaseName("ix_mod_cache_sync_status_provider_content_type");

        builder.Property(s => s.Provider)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(s => s.ContentType)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(s => s.State)
            .IsRequired()
            .HasConversion<string>();

        builder.Property(s => s.LastError)
            .HasMaxLength(4000);

        builder.Property(s => s.UpdatedAt)
            .IsRequired();
    }
}
