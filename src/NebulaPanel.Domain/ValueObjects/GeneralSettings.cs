namespace NebulaPanel.Domain.ValueObjects;

public record GeneralSettings
{
    public string ApplicationName { get; init; } = "Nebula Panel";
    public string? ApplicationUrl { get; init; }
    public string? SupportEmail { get; init; }
    public bool MaintenanceMode { get; init; } = false;
    public string? MaintenanceMessage { get; init; }
}
