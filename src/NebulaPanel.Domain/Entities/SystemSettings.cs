namespace NebulaPanel.Domain.Entities;

public class SystemSettings
{
    public static readonly Guid SingletonId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public Guid Id { get; set; } = SingletonId;
    public string GeneralSettingsJson { get; set; } = "{}";
    public string UpdateSettingsJson { get; set; } = "{}";
    public string AppearanceSettingsJson { get; set; } = "{}";
    public string ModCacheSettingsJson { get; set; } = "{}";
    public string IntegrationSettingsJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastModifiedAt { get; set; } = DateTime.UtcNow;
    public Guid? LastModifiedByUserId { get; set; }

    // Navigation
    public User? LastModifiedByUser { get; set; }
}
