namespace NebulaPanel.Domain.Interfaces;

public interface IIntegrationSettingsProvider
{
    string? GetCurseForgeApiKey();
    string? GetSteamApiKey();
    string? GetModtaleApiKey();
    void InvalidateCache();
}
