namespace NebulaPanel.Domain.Entities;

/// <summary>
/// Records a completed or attempted panel update.
/// </summary>
public class UpdateHistory
{
    public Guid Id { get; set; }
    public string FromVersion { get; set; } = string.Empty;
    public string ToVersion { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ReleaseNotes { get; set; }
    public Guid? InitiatedByUserId { get; set; }
    public User? InitiatedByUser { get; set; }
    public bool WasScheduled { get; set; }
    public Guid? ScheduleId { get; set; }
    public bool WasRolledBack { get; set; }
    public DateTime? RolledBackAt { get; set; }
}
