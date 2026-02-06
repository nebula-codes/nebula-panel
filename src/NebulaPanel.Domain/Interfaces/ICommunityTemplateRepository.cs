using NebulaPanel.Domain.ValueObjects;

namespace NebulaPanel.Domain.Interfaces;

/// <summary>
/// Fetches community game templates from an external repository (e.g., GitHub).
/// </summary>
public interface ICommunityTemplateRepository
{
    Task<IReadOnlyList<CommunityTemplateInfo>> SearchAsync(string? query = null, CancellationToken cancellationToken = default);
    Task<GameTemplate?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default);
}
