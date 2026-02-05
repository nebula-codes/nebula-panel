using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Infrastructure.Persistence.Configurations;

public class ResourceUsageHistoryConfiguration : IEntityTypeConfiguration<ResourceUsageHistory>
{
    public void Configure(EntityTypeBuilder<ResourceUsageHistory> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id)
            .ValueGeneratedNever();

        builder.Property(r => r.Timestamp)
            .IsRequired();

        builder.Property(r => r.CpuPercent)
            .IsRequired();

        builder.Property(r => r.MemoryBytes)
            .IsRequired();

        // PlayerCount is optional
        builder.Property(r => r.PlayerCount);

        // Relationship to GameServer
        builder.HasOne(r => r.Server)
            .WithMany()
            .HasForeignKey(r => r.ServerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indexes for efficient time-range queries
        builder.HasIndex(r => r.ServerId);
        builder.HasIndex(r => r.Timestamp);
        builder.HasIndex(r => new { r.ServerId, r.Timestamp });
    }
}
