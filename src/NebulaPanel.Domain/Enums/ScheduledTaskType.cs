namespace NebulaPanel.Domain.Enums;

public enum ScheduledTaskType
{
    Restart,
    Stop,
    Start,
    Backup,
    Command,        // Execute a console command
    Update          // Check for and apply updates
}
