using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Infrastructure.Persistence.Configurations;

public class ClusterLockConfiguration : IEntityTypeConfiguration<ClusterLock>
{
    public void Configure(EntityTypeBuilder<ClusterLock> builder)
    {
        builder.HasKey(l => l.LockName);

        builder.Property(l => l.LockName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(l => l.HolderNodeId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(l => l.AcquiredAt)
            .IsRequired();

        builder.Property(l => l.ExpiresAt)
            .IsRequired();

        builder.Property(l => l.RowVersion)
            .IsConcurrencyToken()
            .HasDefaultValueSql("randomblob(8)");
    }
}
