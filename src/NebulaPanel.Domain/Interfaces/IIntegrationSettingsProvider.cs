namespace NebulaPanel.Domain.Interfaces;

public interface IIntegrationSettingsProvider
{
    string? GetCurseForgeApiKey();
    string? GetSteamApiKey();
    string? GetModtaleApiKey();
    string? GetSptForgeApiKey();
    void InvalidateCache();
}
