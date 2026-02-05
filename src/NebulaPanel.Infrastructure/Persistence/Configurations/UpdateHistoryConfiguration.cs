using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Infrastructure.Persistence.Configurations;

public class UpdateHistoryConfiguration : IEntityTypeConfiguration<UpdateHistory>
{
    public void Configure(EntityTypeBuilder<UpdateHistory> builder)
    {
        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id)
            .ValueGeneratedNever();

        builder.Property(h => h.FromVersion)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(h => h.ToVersion)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(h => h.StartedAt)
            .IsRequired();

        builder.Property(h => h.Success)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(h => h.ErrorMessage)
            .HasColumnType("TEXT");

        builder.Property(h => h.ReleaseNotes)
            .HasColumnType("TEXT");

        builder.Property(h => h.WasScheduled)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(h => h.WasRolledBack)
            .IsRequired()
            .HasDefaultValue(false);

        // Index on started time for history queries
        builder.HasIndex(h => h.StartedAt);

        // Index on success for filtering
        builder.HasIndex(h => h.Success);

        // Relationship
        builder.HasOne(h => h.InitiatedByUser)
            .WithMany()
            .HasForeignKey(h => h.InitiatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
