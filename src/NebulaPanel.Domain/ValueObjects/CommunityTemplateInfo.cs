namespace NebulaPanel.Domain.ValueObjects;

/// <summary>
/// Lightweight listing record for community templates.
/// </summary>
public record CommunityTemplateInfo(
    string Slug,
    string Name,
    string? Author,
    string? Description,
    List<string> Tags,
    DateTime? UpdatedAt);
