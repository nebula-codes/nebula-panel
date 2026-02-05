using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Infrastructure.Persistence.Configurations;

public class BackupConfiguration : IEntityTypeConfiguration<Backup>
{
    public void Configure(EntityTypeBuilder<Backup> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id)
            .ValueGeneratedNever();

        builder.Property(b => b.Name)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(b => b.FilePath)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(b => b.FileSizeBytes)
            .IsRequired();

        builder.Property(b => b.CreatedAt)
            .IsRequired();

        builder.Property(b => b.Type)
            .IsRequired();

        builder.Property(b => b.Status)
            .IsRequired();

        builder.Property(b => b.Notes)
            .HasMaxLength(1000);

        // Backup scope and content tracking
        builder.Property(b => b.Scope)
            .IsRequired()
            .HasDefaultValue(BackupScope.Full);

        builder.Property(b => b.SourcePaths)
            .HasColumnType("TEXT");

        builder.Property(b => b.ScheduledTaskId);

        builder.Property(b => b.IncludesWorld)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(b => b.IncludesConfig)
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(b => b.IncludesMods)
            .IsRequired()
            .HasDefaultValue(false);

        // Index on server
        builder.HasIndex(b => b.ServerId);

        // Composite index for efficient queries by server and date
        builder.HasIndex(b => new { b.ServerId, b.CreatedAt })
            .IsDescending(false, true);

        // Relationship
        builder.HasOne(b => b.Server)
            .WithMany(s => s.Backups)
            .HasForeignKey(b => b.ServerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
