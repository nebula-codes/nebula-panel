namespace NebulaPanel.Domain.Tests.Entities;

public class HytaleUserCredentialsTests
{
    [Fact]
    public void IsValid_WhenNotExpired_ReturnsTrue()
    {
        // Arrange
        var credentials = new HytaleUserCredentials
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var result = credentials.IsValid;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WhenExpired_ReturnsFalse()
    {
        // Arrange
        var credentials = new HytaleUserCredentials
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1),
            CreatedAt = DateTime.UtcNow.AddHours(-2)
        };

        // Act
        var result = credentials.IsValid;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void TimeRemaining_ReturnsCorrectDuration()
    {
        // Arrange
        var expiresAt = DateTime.UtcNow.AddMinutes(30);
        var credentials = new HytaleUserCredentials
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresAt = expiresAt,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var timeRemaining = credentials.TimeRemaining;

        // Assert - Allow a small margin for test execution time
        timeRemaining.Should().BeCloseTo(TimeSpan.FromMinutes(30), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void TimeRemaining_WhenExpired_ReturnsNegativeDuration()
    {
        // Arrange
        var credentials = new HytaleUserCredentials
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-10),
            CreatedAt = DateTime.UtcNow.AddHours(-2)
        };

        // Act
        var timeRemaining = credentials.TimeRemaining;

        // Assert
        timeRemaining.Should().BeCloseTo(TimeSpan.FromMinutes(-10), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void IsSessionValid_WhenSessionExpiresAtInFuture_ReturnsTrue()
    {
        // Arrange
        var credentials = new HytaleUserCredentials
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            SessionToken = "session-token",
            SessionExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };

        // Act
        var result = credentials.IsSessionValid;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsSessionValid_WhenSessionExpiresAtInPast_ReturnsFalse()
    {
        // Arrange
        var credentials = new HytaleUserCredentials
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            SessionToken = "session-token",
            SessionExpiresAt = DateTime.UtcNow.AddMinutes(-5)
        };

        // Act
        var result = credentials.IsSessionValid;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsSessionValid_WhenSessionExpiresAtIsNull_ReturnsFalse()
    {
        // Arrange
        var credentials = new HytaleUserCredentials
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            SessionToken = "session-token",
            SessionExpiresAt = null
        };

        // Act
        var result = credentials.IsSessionValid;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void SessionTimeRemaining_WhenSessionExpiresAtIsSet_ReturnsCorrectDuration()
    {
        // Arrange
        var sessionExpiresAt = DateTime.UtcNow.AddMinutes(45);
        var credentials = new HytaleUserCredentials
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            SessionExpiresAt = sessionExpiresAt
        };

        // Act
        var sessionTimeRemaining = credentials.SessionTimeRemaining;

        // Assert
        sessionTimeRemaining.Should().NotBeNull();
        sessionTimeRemaining!.Value.Should().BeCloseTo(TimeSpan.FromMinutes(45), TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void SessionTimeRemaining_WhenSessionExpiresAtIsNull_ReturnsNull()
    {
        // Arrange
        var credentials = new HytaleUserCredentials
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            SessionExpiresAt = null
        };

        // Act
        var sessionTimeRemaining = credentials.SessionTimeRemaining;

        // Assert
        sessionTimeRemaining.Should().BeNull();
    }

    [Fact]
    public void HytaleUsername_CanBeSetAndRetrieved()
    {
        // Arrange & Act
        var credentials = new HytaleUserCredentials
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            HytaleUsername = "TestPlayer123"
        };

        // Assert
        credentials.HytaleUsername.Should().Be("TestPlayer123");
    }

    [Fact]
    public void LastRefreshedAt_CanBeSetAndRetrieved()
    {
        // Arrange
        var lastRefreshed = DateTime.UtcNow.AddMinutes(-5);
        var credentials = new HytaleUserCredentials
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            LastRefreshedAt = lastRefreshed
        };

        // Assert
        credentials.LastRefreshedAt.Should().Be(lastRefreshed);
    }

    [Fact]
    public void ProfileUuid_CanBeSetAndRetrieved()
    {
        // Arrange
        var profileUuid = Guid.NewGuid();
        var credentials = new HytaleUserCredentials
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            AccessToken = "access-token",
            RefreshToken = "refresh-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            CreatedAt = DateTime.UtcNow,
            ProfileUuid = profileUuid
        };

        // Assert
        credentials.ProfileUuid.Should().Be(profileUuid);
    }
}
