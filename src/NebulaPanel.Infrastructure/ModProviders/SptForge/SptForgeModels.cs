using System.Text.Json.Serialization;

namespace NebulaPanel.Infrastructure.ModProviders.SptForge;

/// <summary>
/// Standard SPT Forge API response wrapper.
/// </summary>
public record SptForgeResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("data")]
    public T? Data { get; init; }

    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

/// <summary>
/// Paginated list response from SPT Forge.
/// </summary>
public record SptForgePaginatedResponse<T>
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("data")]
    public T[]? Data { get; init; }

    [JsonPropertyName("meta")]
    public SptForgePaginationMeta? Meta { get; init; }
}

public record SptForgePaginationMeta
{
    [JsonPropertyName("current_page")]
    public int CurrentPage { get; init; }

    [JsonPropertyName("per_page")]
    public int PerPage { get; init; }

    [JsonPropertyName("total")]
    public int Total { get; init; }

    [JsonPropertyName("last_page")]
    public int LastPage { get; init; }
}

/// <summary>
/// SPT Forge mod summary (from list/search endpoint).
/// </summary>
public record SptForgeMod
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("slug")]
    public string Slug { get; init; } = string.Empty;

    [JsonPropertyName("teaser")]
    public string? Teaser { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; init; }

    [JsonPropertyName("downloads")]
    public long Downloads { get; init; }

    [JsonPropertyName("published_at")]
    public string? PublishedAt { get; init; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; init; }

    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; init; }

    [JsonPropertyName("owner")]
    public SptForgeUser? User { get; init; }

    [JsonPropertyName("category")]
    public SptForgeCategory? Category { get; init; }

    [JsonPropertyName("versions")]
    public SptForgeModVersion[]? Versions { get; init; }

    [JsonPropertyName("fika_compatibility")]
    public object? FikaCompatibility { get; init; }

    [JsonPropertyName("source_code_links")]
    public SptForgeSourceCodeLink[]? SourceCodeLinks { get; init; }
}

public record SptForgeSourceCodeLink
{
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("label")]
    public string? Label { get; init; }
}

public record SptForgeUser
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}

public record SptForgeCategory
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("slug")]
    public string? Slug { get; init; }
}

/// <summary>
/// SPT Forge mod version.
/// </summary>
public record SptForgeModVersion
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("mod_id")]
    public int? ModId { get; init; }

    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("spt_version_constraint")]
    public string? SptVersion { get; init; }

    [JsonPropertyName("link")]
    public string? DownloadUrl { get; init; }

    [JsonPropertyName("content_length")]
    public long? FileSize { get; init; }

    [JsonPropertyName("downloads")]
    public long? Downloads { get; init; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; init; }

    [JsonPropertyName("fika_compatibility")]
    public object? FikaCompatibility { get; init; }

    [JsonPropertyName("dependencies")]
    public SptForgeDependency[]? Dependencies { get; init; }
}

public record SptForgeDependency
{
    [JsonPropertyName("mod_id")]
    public int ModId { get; init; }

    [JsonPropertyName("mod_name")]
    public string? ModName { get; init; }

    [JsonPropertyName("version_id")]
    public int? VersionId { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; } = "required";
}

/// <summary>
/// SPT version info from the spt-versions endpoint.
/// </summary>
public record SptForgeVersionInfo
{
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("color_class")]
    public string? ColorClass { get; init; }
}
