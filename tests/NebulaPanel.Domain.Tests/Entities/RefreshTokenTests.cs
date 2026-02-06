namespace NebulaPanel.Domain.Tests.Entities;

public class RefreshTokenTests
{
    [Fact]
    public void IsExpired_WhenExpiresAtInPast_ReturnsTrue()
    {
        // Arrange
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TokenHash = "test-token-hash",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };

        // Act
        var result = token.IsExpired;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsExpired_WhenExpiresAtInFuture_ReturnsFalse()
    {
        // Arrange
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TokenHash = "test-token-hash",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var result = token.IsExpired;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_WhenExpiresAtIsNow_ReturnsTrue()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TokenHash = "test-token-hash",
            ExpiresAt = now,
            CreatedAt = now.AddMinutes(-5)
        };

        // Act & Assert - ExpiresAt should be expired (>= check)
        // Note: Due to timing, we accept either true or false at exact boundary
        token.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void IsRevoked_WhenRevokedAtIsSet_ReturnsTrue()
    {
        // Arrange
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TokenHash = "test-token-hash",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            RevokedAt = DateTime.UtcNow
        };

        // Act
        var result = token.IsRevoked;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsRevoked_WhenRevokedAtIsNull_ReturnsFalse()
    {
        // Arrange
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TokenHash = "test-token-hash",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            RevokedAt = null
        };

        // Act
        var result = token.IsRevoked;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsActive_WhenNotExpiredAndNotRevoked_ReturnsTrue()
    {
        // Arrange
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TokenHash = "test-token-hash",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            RevokedAt = null
        };

        // Act
        var result = token.IsActive;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsActive_WhenExpired_ReturnsFalse()
    {
        // Arrange
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TokenHash = "test-token-hash",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            RevokedAt = null
        };

        // Act
        var result = token.IsActive;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsActive_WhenRevoked_ReturnsFalse()
    {
        // Arrange
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TokenHash = "test-token-hash",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            RevokedAt = DateTime.UtcNow
        };

        // Act
        var result = token.IsActive;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsActive_WhenBothExpiredAndRevoked_ReturnsFalse()
    {
        // Arrange
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TokenHash = "test-token-hash",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            RevokedAt = DateTime.UtcNow.AddMinutes(-5)
        };

        // Act
        var result = token.IsActive;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ReplacedByTokenHash_CanBeSetAndRetrieved()
    {
        // Arrange
        var token = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TokenHash = "old-token-hash",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            ReplacedByTokenHash = "new-token-hash"
        };

        // Assert
        token.ReplacedByTokenHash.Should().Be("new-token-hash");
    }
}
