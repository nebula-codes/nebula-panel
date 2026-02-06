using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NebulaPanel.Domain.Interfaces;
using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Infrastructure.Templates;

public class GitHubCommunityTemplateRepository(
    IHttpClientFactory httpClientFactory,
    IMemoryCache memoryCache,
    IConfiguration configuration,
    ILogger<GitHubCommunityTemplateRepository> logger) : ICommunityTemplateRepository
{
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly IMemoryCache _memoryCache = memoryCache;
    private readonly ILogger<GitHubCommunityTemplateRepository> _logger = logger;

    private const string IndexCacheKey = "community_templates:index";
    private const string TemplateCachePrefix = "community_templates:template:";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private string GetBaseUrl()
    {
        return configuration["CommunityTemplates:BaseUrl"]
               ?? "https://raw.githubusercontent.com/nebula-panel/game-templates/main";
    }

    public async Task<IReadOnlyList<CommunityTemplateInfo>> SearchAsync(string? query = null, CancellationToken cancellationToken = default)
    {
        var index = await GetIndexAsync(cancellationToken).ConfigureAwait(false);
        if (index is null)
            return [];

        if (string.IsNullOrWhiteSpace(query))
            return index;

        return index.Where(t =>
            t.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            (t.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
            t.Tags.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase))
        ).ToList();
    }

    public async Task<GameTemplate?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{TemplateCachePrefix}{slug}";
        if (_memoryCache.TryGetValue<GameTemplate>(cacheKey, out var cached) && cached is not null)
            return cached;

        try
        {
            var client = _httpClientFactory.CreateClient("CommunityTemplates");
            var baseUrl = GetBaseUrl();
            var url = $"{baseUrl}/templates/{slug}/game.json";

            var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Community template {Slug} not found (HTTP {StatusCode})", slug, response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var template = JsonSerializer.Deserialize<GameTemplate>(json, JsonOptions);

            if (template is not null)
            {
                _memoryCache.Set(cacheKey, template, CacheDuration);
            }

            return template;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch community template {Slug}", slug);
            return null;
        }
    }

    private async Task<IReadOnlyList<CommunityTemplateInfo>?> GetIndexAsync(CancellationToken cancellationToken)
    {
        if (_memoryCache.TryGetValue<IReadOnlyList<CommunityTemplateInfo>>(IndexCacheKey, out var cached) && cached is not null)
            return cached;

        try
        {
            var client = _httpClientFactory.CreateClient("CommunityTemplates");
            var baseUrl = GetBaseUrl();
            var url = $"{baseUrl}/index.json";

            var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch community template index (HTTP {StatusCode})", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var index = JsonSerializer.Deserialize<List<CommunityTemplateInfo>>(json, JsonOptions);

            if (index is not null)
            {
                _memoryCache.Set(IndexCacheKey, (IReadOnlyList<CommunityTemplateInfo>)index, CacheDuration);
            }

            return index;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch community template index");
            return null;
        }
    }
}
