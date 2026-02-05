namespace NebulaPanel.Domain.Entities;

/// <summary>
/// Represents a scheduled panel update.
/// </summary>
public class UpdateSchedule
{
    public Guid Id { get; set; }
    public string TargetVersion { get; set; } = string.Empty;
    public DateTime ScheduledAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid CreatedByUserId { get; set; }
    private User? _createdByUser;
    public User CreatedByUser
    {
        get => _createdByUser ?? throw new InvalidOperationException("CreatedByUser navigation property was not loaded. Use .Include(x => x.CreatedByUser) in your query.");
        set => _createdByUser = value;
    }
    public bool CreateBackup { get; set; } = true;
    public bool StopGameServers { get; set; } = true;
    public bool RestartGameServers { get; set; } = true;
    public bool IsCancelled { get; set; }
    public DateTime? CancelledAt { get; set; }
    public Guid? CancelledByUserId { get; set; }
    public User? CancelledByUser { get; set; }
    public string? ReleaseNotes { get; set; }
    public bool IsExecuted { get; set; }
    public DateTime? ExecutedAt { get; set; }
}
