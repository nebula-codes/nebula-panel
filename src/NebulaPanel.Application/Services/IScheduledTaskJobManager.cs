using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Application.Services;

public interface IScheduledTaskJobManager
{
    /// <summary>
    /// Registers or updates a recurring job in the job scheduler.
    /// </summary>
    void RegisterRecurringJob(ScheduledTask task);

    /// <summary>
    /// Removes a recurring job from the job scheduler.
    /// </summary>
    void RemoveRecurringJob(Guid taskId);

    /// <summary>
    /// Enqueues a task for immediate execution.
    /// </summary>
    void EnqueueTask(Guid taskId);
}
