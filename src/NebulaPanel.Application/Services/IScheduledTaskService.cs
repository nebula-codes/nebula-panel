using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;

namespace NebulaPanel.Application.Services;

public interface IScheduledTaskService
{
    // CRUD operations
    Task<IReadOnlyList<ScheduledTaskListItemDto>> GetAllTasksAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScheduledTaskListItemDto>> GetTasksByServerIdAsync(Guid serverId, CancellationToken cancellationToken = default);
    Task<Result<ScheduledTaskDto>> GetTaskByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<ScheduledTaskDto>> CreateTaskAsync(CreateScheduledTaskRequest request, CancellationToken cancellationToken = default);
    Task<Result<ScheduledTaskDto>> UpdateTaskAsync(Guid id, UpdateScheduledTaskRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteTaskAsync(Guid id, CancellationToken cancellationToken = default);

    // Task control
    Task<Result> EnableTaskAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result> DisableTaskAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result> RunTaskNowAsync(Guid id, CancellationToken cancellationToken = default);

    // Execution (called by Hangfire)
    Task ExecuteTaskAsync(Guid taskId, CancellationToken cancellationToken = default);

    // Hangfire synchronization
    Task SynchronizeJobsAsync(CancellationToken cancellationToken = default);
}
