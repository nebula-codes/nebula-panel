using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Infrastructure.Persistence.Configurations;

public class ScheduledTaskConfiguration : IEntityTypeConfiguration<ScheduledTask>
{
    public void Configure(EntityTypeBuilder<ScheduledTask> builder)
    {
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id)
            .ValueGeneratedNever();

        builder.Property(t => t.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.TaskType)
            .IsRequired();

        builder.Property(t => t.CronExpression)
            .HasMaxLength(100);

        builder.Property(t => t.IsEnabled)
            .IsRequired()
            .HasDefaultValue(true);

        // Configuration stored as JSON string (already a string in entity)
        builder.Property(t => t.Configuration)
            .HasColumnType("TEXT");

        // Index on server
        builder.HasIndex(t => t.ServerId);

        // Relationship
        builder.HasOne(t => t.Server)
            .WithMany(s => s.ScheduledTasks)
            .HasForeignKey(t => t.ServerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
