using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Infrastructure.Persistence.Configurations;

public class HytaleUserCredentialsConfiguration : IEntityTypeConfiguration<HytaleUserCredentials>
{
    public void Configure(EntityTypeBuilder<HytaleUserCredentials> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id)
            .ValueGeneratedNever();

        builder.Property(c => c.AccessToken)
            .IsRequired()
            .HasMaxLength(4096);  // Encrypted tokens can be long

        builder.Property(c => c.RefreshToken)
            .IsRequired()
            .HasMaxLength(4096);  // Encrypted tokens can be long

        builder.Property(c => c.ExpiresAt)
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.HytaleUsername)
            .HasMaxLength(100);

        // Session tokens (Method B - direct OAuth)
        builder.Property(c => c.SessionToken)
            .HasMaxLength(4096);  // Encrypted JWT can be long

        builder.Property(c => c.IdentityToken)
            .HasMaxLength(4096);  // Encrypted JWT can be long

        builder.Property(c => c.SessionExpiresAt);

        builder.Property(c => c.ProfileUuid);

        builder.Property(c => c.OwnerUuid);

        // Unique index on UserId - one credential per user
        builder.HasIndex(c => c.UserId)
            .IsUnique();

        // Relationship with User
        builder.HasOne(c => c.User)
            .WithOne(u => u.HytaleCredentials)
            .HasForeignKey<HytaleUserCredentials>(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ignore computed properties
        builder.Ignore(c => c.IsValid);
        builder.Ignore(c => c.TimeRemaining);
        builder.Ignore(c => c.IsSessionValid);
        builder.Ignore(c => c.SessionTimeRemaining);
    }
}
