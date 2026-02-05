using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Domain.ValueObjects;

public record UpdateSettings
{
    public bool Enabled { get; init; } = true;
    public bool AutoCheck { get; init; } = true;
    public int CheckIntervalHours { get; init; } = 6;
    public UpdateChannel Channel { get; init; } = UpdateChannel.Stable;
    public DateTime? LastCheckAt { get; init; }
    public string? LatestAvailableVersion { get; init; }
}
