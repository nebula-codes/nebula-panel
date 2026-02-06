namespace NebulaPanel.Domain.Enums;

public enum WebhookEventType
{
    ServerStarted = 0,
    ServerStopped = 1,
    ServerCrashed = 2,
    BackupCompleted = 3,
    BackupFailed = 4,
    UpdateAvailable = 5,
    ServerInstalled = 6,
    ServerDeleted = 7
}
