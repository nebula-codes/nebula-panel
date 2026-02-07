namespace NebulaPanel.Domain.ValueObjects;

public record IntegrationSettings
{
    public string? CurseForgeApiKey { get; init; }
    public string? SteamApiKey { get; init; }
    public string? ModtaleApiKey { get; init; }
}
