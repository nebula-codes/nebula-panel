using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Infrastructure.Persistence.Configurations;

public class UpdateScheduleConfiguration : IEntityTypeConfiguration<UpdateSchedule>
{
    public void Configure(EntityTypeBuilder<UpdateSchedule> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .ValueGeneratedNever();

        builder.Property(s => s.TargetVersion)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(s => s.ScheduledAt)
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.CreateBackup)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(s => s.StopGameServers)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(s => s.RestartGameServers)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(s => s.IsCancelled)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.IsExecuted)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(s => s.ReleaseNotes)
            .HasColumnType("TEXT");

        // Index on scheduled time for efficient queries
        builder.HasIndex(s => s.ScheduledAt);

        // Index on status for pending schedules
        builder.HasIndex(s => new { s.IsCancelled, s.IsExecuted, s.ScheduledAt });

        // Relationships
        builder.HasOne(s => s.CreatedByUser)
            .WithMany()
            .HasForeignKey(s => s.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(s => s.CancelledByUser)
            .WithMany()
            .HasForeignKey(s => s.CancelledByUserId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
