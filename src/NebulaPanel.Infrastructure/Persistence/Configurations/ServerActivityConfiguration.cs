using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Infrastructure.Persistence.Configurations;

public class ServerActivityConfiguration : IEntityTypeConfiguration<ServerActivity>
{
    public void Configure(EntityTypeBuilder<ServerActivity> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id)
            .ValueGeneratedNever();

        builder.Property(a => a.Timestamp)
            .IsRequired();

        builder.Property(a => a.ActivityType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(a => a.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.Metadata)
            .HasColumnType("TEXT");

        // Relationship to GameServer
        builder.HasOne(a => a.Server)
            .WithMany()
            .HasForeignKey(a => a.ServerId)
            .OnDelete(DeleteBehavior.Cascade);

        // Relationship to User (optional - null for system events)
        builder.HasOne(a => a.User)
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes for efficient queries
        builder.HasIndex(a => a.ServerId);
        builder.HasIndex(a => a.Timestamp);
        builder.HasIndex(a => new { a.ServerId, a.Timestamp });
    }
}
