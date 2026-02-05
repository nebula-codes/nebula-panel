namespace NebulaPanel.Application.Tests.Builders;

/// <summary>
/// Fluent builder for creating User test instances.
/// </summary>
public class UserBuilder
{
    private readonly User _user;

    public UserBuilder()
    {
        _user = new User
        {
            Id = Guid.NewGuid(),
            Username = "testuser",
            Email = "test@example.com",
            PasswordHash = "hashedpassword",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public UserBuilder WithId(Guid id)
    {
        _user.Id = id;
        return this;
    }

    public UserBuilder WithUsername(string username)
    {
        _user.Username = username;
        return this;
    }

    public UserBuilder WithEmail(string email)
    {
        _user.Email = email;
        return this;
    }

    public UserBuilder WithPasswordHash(string passwordHash)
    {
        _user.PasswordHash = passwordHash;
        return this;
    }

    public UserBuilder AsActive()
    {
        _user.IsActive = true;
        return this;
    }

    public UserBuilder AsInactive()
    {
        _user.IsActive = false;
        return this;
    }

    public UserBuilder WithLastLoginAt(DateTime? lastLoginAt)
    {
        _user.LastLoginAt = lastLoginAt;
        return this;
    }

    public UserBuilder WithCreatedAt(DateTime createdAt)
    {
        _user.CreatedAt = createdAt;
        return this;
    }

    public User Build() => _user;
}
