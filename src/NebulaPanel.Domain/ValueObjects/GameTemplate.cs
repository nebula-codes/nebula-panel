using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Domain.ValueObjects;

/// <summary>
/// A portable game template wrapping a JsonGameDefinition with metadata.
/// Templates are ephemeral JSON documents — not persisted in the database.
/// </summary>
public record GameTemplate
{
    public required JsonGameDefinition Definition { get; init; }
    public string? Author { get; init; }
    public string? Description { get; init; }
    public List<string> Tags { get; init; } = [];
    public string TemplateVersion { get; init; } = "1.0";
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a GameTemplate from an existing Game entity.
    /// </summary>
    public static GameTemplate FromGame(Game game, string? author = null, string? description = null, List<string>? tags = null)
    {
        var definition = new JsonGameDefinition
        {
            Slug = game.Slug,
            Name = game.Name,
            SteamAppId = game.SteamAppId,
            ExecutableType = game.ExecutableType,
            DefaultExecutablePath = game.DefaultExecutablePath,
            DefaultStartCommand = game.DefaultStartCommand,
            DefaultStopCommand = game.DefaultStopCommand,
            SupportsDocker = game.SupportsDocker,
            DefaultDockerImage = game.DefaultDockerImage,
            SupportsMods = game.SupportsMods,
            ModProviders = game.ModProviders,
            RconDefaults = game.RconDefaults,
            IconPath = game.IconPath
        };

        return new GameTemplate
        {
            Definition = definition,
            Author = author,
            Description = description,
            Tags = tags ?? [],
            CreatedAt = DateTime.UtcNow
        };
    }
}
