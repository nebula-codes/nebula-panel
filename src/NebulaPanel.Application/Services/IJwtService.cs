namespace NebulaPanel.Application.Services;

using NebulaPanel.Domain.Entities;

public interface IJwtService
{
    string GenerateAccessToken(User user, IReadOnlyList<string> roles, IReadOnlyList<string> permissions);
    string GenerateRefreshToken();
    (Guid userId, DateTime expiration)? ValidateAccessToken(string token);
    (Guid userId, DateTime expiration)? ValidateRefreshToken(string token);
}
