using Hangfire;
using NebulaPanel.Application.Services;
using NebulaPanel.Domain.Entities;

namespace NebulaPanel.Infrastructure.Services;

public class HangfireJobManager : IScheduledTaskJobManager
{
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly IBackgroundJobClient _backgroundJobClient;

    public HangfireJobManager(
        IRecurringJobManager recurringJobManager,
        IBackgroundJobClient backgroundJobClient)
    {
        _recurringJobManager = recurringJobManager;
        _backgroundJobClient = backgroundJobClient;
    }

    public void RegisterRecurringJob(ScheduledTask task)
    {
        if (string.IsNullOrEmpty(task.CronExpression))
            return;

        var jobId = GetJobId(task.Id);

        _recurringJobManager.AddOrUpdate<IScheduledTaskService>(
            jobId,
            service => service.ExecuteTaskAsync(task.Id, CancellationToken.None),
            task.CronExpression,
            new RecurringJobOptions
            {
                TimeZone = TimeZoneInfo.Utc
            });
    }

    public void RemoveRecurringJob(Guid taskId)
    {
        var jobId = GetJobId(taskId);
        _recurringJobManager.RemoveIfExists(jobId);
    }

    public void EnqueueTask(Guid taskId)
    {
        _backgroundJobClient.Enqueue<IScheduledTaskService>(
            service => service.ExecuteTaskAsync(taskId, CancellationToken.None));
    }

    private static string GetJobId(Guid taskId) => $"scheduled-task-{taskId}";
}
