namespace NebulaPanel.Domain.Enums;

public enum BackupType
{
    Manual,         // User-initiated backup
    Scheduled,      // Created by scheduled task
    PreUpdate,      // Automatic backup before server update
    PreRestore      // Automatic backup before restoring another backup
}
