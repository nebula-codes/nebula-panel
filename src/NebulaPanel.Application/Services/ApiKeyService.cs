using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Repositories;

namespace NebulaPanel.Application.Services;

public class ApiKeyService(
    IApiKeyRepository apiKeyRepository,
    IAuditService? auditService,
    ILogger<ApiKeyService> logger) : IApiKeyService
{
    private readonly IApiKeyRepository _apiKeyRepository = apiKeyRepository;
    private readonly IAuditService? _auditService = auditService;
    private readonly ILogger<ApiKeyService> _logger = logger;

    public async Task<IReadOnlyList<ApiKeyDto>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var keys = await _apiKeyRepository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);
        return keys.Select(MapToDto).ToList();
    }

    public async Task<Result<ApiKeyCreatedDto>> CreateAsync(CreateApiKeyRequest request, Guid userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return Result.Failure<ApiKeyCreatedDto>(Error.Validation("API key name is required."));

        // Generate raw key: np_{base62_random}
        var rawKey = $"np_{GenerateRandomBase62(40)}";
        var keyHash = HashKey(rawKey);
        var keyPrefix = rawKey[..11]; // "np_" + first 8 chars

        var apiKey = new ApiKey
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = request.Name,
            KeyHash = keyHash,
            KeyPrefix = keyPrefix,
            Scopes = request.Scopes?.ToList() ?? [],
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = request.ExpiresInDays.HasValue
                ? DateTime.UtcNow.AddDays(request.ExpiresInDays.Value)
                : null
        };

        await _apiKeyRepository.AddAsync(apiKey, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Created API key {KeyName} ({KeyPrefix}...) for user {UserId}", apiKey.Name, apiKey.KeyPrefix, userId);

        if (_auditService is not null)
        {
            await _auditService.LogEventAsync(
                SecurityEventType.ApiKeyCreated, userId, null, true,
                $"API key '{apiKey.Name}' created", cancellationToken).ConfigureAwait(false);
        }

        return new ApiKeyCreatedDto(
            apiKey.Id, apiKey.Name, apiKey.KeyPrefix, rawKey,
            apiKey.Scopes, apiKey.CreatedAt, apiKey.ExpiresAt);
    }

    public async Task<Result> RevokeAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
    {
        var apiKey = await _apiKeyRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (apiKey is null)
            return Result.Failure(Error.NotFound("API key", id.ToString()));

        if (apiKey.UserId != userId)
            return Result.Failure(Error.Forbidden());

        if (apiKey.IsRevoked)
            return Result.Failure(Error.InvalidOperation("API key is already revoked."));

        apiKey.RevokedAt = DateTime.UtcNow;
        await _apiKeyRepository.UpdateAsync(apiKey, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Revoked API key {KeyName} ({KeyPrefix}...) for user {UserId}", apiKey.Name, apiKey.KeyPrefix, userId);

        if (_auditService is not null)
        {
            await _auditService.LogEventAsync(
                SecurityEventType.ApiKeyRevoked, userId, null, true,
                $"API key '{apiKey.Name}' revoked", cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    public async Task<Result<Guid>> ValidateAsync(string rawKey, CancellationToken cancellationToken = default)
    {
        var keyHash = HashKey(rawKey);
        var apiKey = await _apiKeyRepository.GetByKeyHashAsync(keyHash, cancellationToken).ConfigureAwait(false);

        if (apiKey is null)
            return Result.Failure<Guid>(Error.Unauthorized("Invalid API key."));

        if (!apiKey.IsActive)
            return Result.Failure<Guid>(Error.Unauthorized("API key is revoked or expired."));

        apiKey.LastUsedAt = DateTime.UtcNow;
        await _apiKeyRepository.UpdateAsync(apiKey, cancellationToken).ConfigureAwait(false);

        return apiKey.UserId;
    }

    private static string HashKey(string rawKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawKey));
        return Convert.ToHexStringLower(hash);
    }

    private static string GenerateRandomBase62(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var result = new char[length];
        for (var i = 0; i < length; i++)
        {
            result[i] = chars[RandomNumberGenerator.GetInt32(chars.Length)];
        }
        return new string(result);
    }

    private static ApiKeyDto MapToDto(ApiKey key) => new(
        key.Id, key.Name, key.KeyPrefix, key.Scopes,
        key.CreatedAt, key.ExpiresAt, key.LastUsedAt, key.IsRevoked);
}
