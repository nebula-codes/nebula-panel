namespace NebulaPanel.Application.DTOs;

public record ApiKeyDto(
    Guid Id,
    string Name,
    string KeyPrefix,
    IReadOnlyList<string> Scopes,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    DateTime? LastUsedAt,
    bool IsRevoked);

public record ApiKeyCreatedDto(
    Guid Id,
    string Name,
    string KeyPrefix,
    string RawKey,
    IReadOnlyList<string> Scopes,
    DateTime CreatedAt,
    DateTime? ExpiresAt);

public record CreateApiKeyRequest(
    string Name,
    IReadOnlyList<string>? Scopes = null,
    int? ExpiresInDays = null);
