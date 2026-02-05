using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Infrastructure.Persistence.Configurations;

public class ServerPermissionConfiguration : IEntityTypeConfiguration<ServerPermission>
{
    public void Configure(EntityTypeBuilder<ServerPermission> builder)
    {
        builder.HasKey(sp => sp.Id);

        builder.Property(sp => sp.Id)
            .ValueGeneratedNever();

        builder.Property(sp => sp.PermissionCode)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(sp => sp.IsGranted)
            .IsRequired();

        // Unique constraint: one permission per user per server per code
        builder.HasIndex(sp => new { sp.UserId, sp.ServerId, sp.PermissionCode })
            .IsUnique();

        // Relationships
        builder.HasOne(sp => sp.User)
            .WithMany(u => u.ServerPermissions)
            .HasForeignKey(sp => sp.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(sp => sp.Server)
            .WithMany()
            .HasForeignKey(sp => sp.ServerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
