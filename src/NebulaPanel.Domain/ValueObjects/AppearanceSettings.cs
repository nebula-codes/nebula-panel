namespace NebulaPanel.Domain.ValueObjects;

public record AppearanceSettings
{
    public string DefaultTheme { get; init; } = "dark";
    public bool AllowUserThemeOverride { get; init; } = true;
    public string AccentColor { get; init; } = "purple";
}
