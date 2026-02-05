using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;

namespace NebulaPanel.Domain.Entities;

public class CachedMod
{
    public Guid Id { get; set; }
    public ModProviderType Provider { get; set; }
    public string ProviderModId { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string? IconUrl { get; set; }
    public string? Author { get; set; }
    public long Downloads { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ModContentType ContentType { get; set; }
    public string CategoriesJson { get; set; } = "[]";
    public string GameVersionsJson { get; set; } = "[]";
    public string LoadersJson { get; set; } = "[]";
    public DateTime CachedAt { get; set; }
    public string? DescriptionHtml { get; set; }
    public string? VersionsJson { get; set; }
}
