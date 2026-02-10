using System.Text.Json;
using Microsoft.Extensions.Logging;
using NebulaPanel.Application.Common;
using NebulaPanel.Application.DTOs;
using NebulaPanel.Domain.Entities;
using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Repositories;

namespace NebulaPanel.Application.Services;

public class ScheduledTaskService(
    IScheduledTaskRepository taskRepository,
    IGameServerRepository serverRepository,
    IGameServerService serverService,
    IBackupService backupService,
    IScheduledTaskJobManager jobManager,
    ILogger<ScheduledTaskService> logger) : IScheduledTaskService
{
    private readonly IScheduledTaskRepository _taskRepository = taskRepository;
    private readonly IGameServerRepository _serverRepository = serverRepository;
    private readonly IGameServerService _serverService = serverService;
    private readonly IBackupService _backupService = backupService;
    private readonly IScheduledTaskJobManager _jobManager = jobManager;
    private readonly ILogger<ScheduledTaskService> _logger = logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<IReadOnlyList<ScheduledTaskListItemDto>> GetAllTasksAsync(CancellationToken cancellationToken = default)
    {
        var tasks = await _taskRepository.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return tasks.Select(MapToListItemDto).ToList();
    }

    public async Task<IReadOnlyList<ScheduledTaskListItemDto>> GetTasksByServerIdAsync(Guid serverId, CancellationToken cancellationToken = default)
    {
        var tasks = await _taskRepository.GetByServerIdAsync(serverId, cancellationToken).ConfigureAwait(false);
        return tasks.Select(MapToListItemDto).ToList();
    }

    public async Task<Result<ScheduledTaskDto>> GetTaskByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var task = await _taskRepository.GetByIdWithServerAsync(id, cancellationToken).ConfigureAwait(false);
        if (task is null)
        {
            return Result.Failure<ScheduledTaskDto>(Error.NotFound("ScheduledTask", id.ToString()));
        }
        return MapToDto(task);
    }

    public async Task<Result<ScheduledTaskDto>> CreateTaskAsync(CreateScheduledTaskRequest request, CancellationToken cancellationToken = default)
    {
        // Validate server exists
        var server = await _serverRepository.GetByIdAsync(request.ServerId, cancellationToken).ConfigureAwait(false);
        if (server is null)
        {
            return Result.Failure<ScheduledTaskDto>(Error.NotFound("Server", request.ServerId.ToString()));
        }

        // Validate unique name per server
        if (await _taskRepository.NameExistsForServerAsync(request.Name, request.ServerId, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ScheduledTaskDto>(Error.AlreadyExists("ScheduledTask", request.Name));
        }

        // Validate cron expression if provided
        if (!string.IsNullOrEmpty(request.CronExpression))
        {
            var cronResult = CronValidator.ValidateCronExpression(request.CronExpression);
            if (cronResult.IsFailure)
            {
                return Result.Failure<ScheduledTaskDto>(cronResult.Error!);
            }
        }

        var task = new ScheduledTask
        {
            Id = Guid.NewGuid(),
            ServerId = request.ServerId,
            Name = request.Name,
            TaskType = request.TaskType,
            CronExpression = request.CronExpression,
            IsEnabled = request.IsEnabled,
            Configuration = request.Configuration,
            NextRunAt = !string.IsNullOrEmpty(request.CronExpression)
                ? CronValidator.GetNextOccurrence(request.CronExpression, DateTime.UtcNow)
                : null
        };

        await _taskRepository.AddAsync(task, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Created scheduled task {TaskName} for server {ServerId}", task.Name, task.ServerId);

        // Register Hangfire job if enabled and has cron expression
        if (task.IsEnabled && !string.IsNullOrEmpty(task.CronExpression))
        {
            _jobManager.RegisterRecurringJob(task);
        }

        var createdTask = await _taskRepository.GetByIdWithServerAsync(task.Id, cancellationToken).ConfigureAwait(false);
        return MapToDto(createdTask!);
    }

    public async Task<Result<ScheduledTaskDto>> UpdateTaskAsync(Guid id, UpdateScheduledTaskRequest request, CancellationToken cancellationToken = default)
    {
        var task = await _taskRepository.GetByIdWithServerAsync(id, cancellationToken).ConfigureAwait(false);
        if (task is null)
        {
            return Result.Failure<ScheduledTaskDto>(Error.NotFound("ScheduledTask", id.ToString()));
        }

        // Validate unique name
        if (await _taskRepository.NameExistsForServerAsync(request.Name, task.ServerId, id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<ScheduledTaskDto>(Error.AlreadyExists("ScheduledTask", request.Name));
        }

        // Validate cron expression if provided
        if (!string.IsNullOrEmpty(request.CronExpression))
        {
            var cronResult = CronValidator.ValidateCronExpression(request.CronExpression);
            if (cronResult.IsFailure)
            {
                return Result.Failure<ScheduledTaskDto>(cronResult.Error!);
            }
        }

        var wasEnabled = task.IsEnabled;
        var oldCron = task.CronExpression;

        task.Name = request.Name;
        task.TaskType = request.TaskType;
        task.CronExpression = request.CronExpression;
        task.IsEnabled = request.IsEnabled;
        task.Configuration = request.Configuration;
        task.NextRunAt = !string.IsNullOrEmpty(request.CronExpression)
            ? CronValidator.GetNextOccurrence(request.CronExpression, DateTime.UtcNow)
            : null;

        await _taskRepository.UpdateAsync(task, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Updated scheduled task {TaskName} ({TaskId})", task.Name, task.Id);

        // Update Hangfire job
        if (task.IsEnabled && !string.IsNullOrEmpty(task.CronExpression))
        {
            _jobManager.RegisterRecurringJob(task);
        }
        else if (wasEnabled || !string.IsNullOrEmpty(oldCron))
        {
            _jobManager.RemoveRecurringJob(task.Id);
        }

        return MapToDto(task);
    }

    public async Task<Result> DeleteTaskAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var task = await _taskRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (task is null)
        {
            return Result.Failure(Error.NotFound("ScheduledTask", id.ToString()));
        }

        // Remove Hangfire job
        _jobManager.RemoveRecurringJob(id);

        await _taskRepository.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Deleted scheduled task {TaskName} ({TaskId})", task.Name, task.Id);

        return Result.Success();
    }

    public async Task<Result> EnableTaskAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var task = await _taskRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (task is null)
        {
            return Result.Failure(Error.NotFound("ScheduledTask", id.ToString()));
        }

        task.IsEnabled = true;
        task.NextRunAt = !string.IsNullOrEmpty(task.CronExpression)
            ? CronValidator.GetNextOccurrence(task.CronExpression, DateTime.UtcNow)
            : null;

        await _taskRepository.UpdateAsync(task, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Enabled scheduled task {TaskName} ({TaskId})", task.Name, task.Id);

        if (!string.IsNullOrEmpty(task.CronExpression))
        {
            _jobManager.RegisterRecurringJob(task);
        }

        return Result.Success();
    }

    public async Task<Result> DisableTaskAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var task = await _taskRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (task is null)
        {
            return Result.Failure(Error.NotFound("ScheduledTask", id.ToString()));
        }

        task.IsEnabled = false;
        task.NextRunAt = null;

        await _taskRepository.UpdateAsync(task, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Disabled scheduled task {TaskName} ({TaskId})", task.Name, task.Id);

        _jobManager.RemoveRecurringJob(id);

        return Result.Success();
    }

    public async Task<Result> RunTaskNowAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var task = await _taskRepository.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (task is null)
        {
            return Result.Failure(Error.NotFound("ScheduledTask", id.ToString()));
        }

        _logger.LogInformation("Manually triggered scheduled task {TaskName} ({TaskId})", task.Name, task.Id);
        _jobManager.EnqueueTask(id);

        return Result.Success();
    }

    public async Task ExecuteTaskAsync(Guid taskId, CancellationToken cancellationToken = default)
    {
        var task = await _taskRepository.GetByIdWithServerAsync(taskId, cancellationToken).ConfigureAwait(false);
        if (task is null)
        {
            _logger.LogWarning("Attempted to execute non-existent task {TaskId}", taskId);
            return;
        }

        if (!task.IsEnabled)
        {
            _logger.LogDebug("Skipping disabled task {TaskName} ({TaskId})", task.Name, task.Id);
            return;
        }

        _logger.LogInformation("Executing scheduled task {TaskName} ({TaskType}) for server {ServerName}",
            task.Name, task.TaskType, task.Server.Name);

        try
        {
            var result = task.TaskType switch
            {
                ScheduledTaskType.Restart => await _serverService.RestartServerAsync(task.ServerId, cancellationToken).ConfigureAwait(false),
                ScheduledTaskType.Stop => await _serverService.StopServerAsync(task.ServerId, cancellationToken).ConfigureAwait(false),
                ScheduledTaskType.Start => await _serverService.StartServerAsync(task.ServerId, cancellationToken).ConfigureAwait(false),
                ScheduledTaskType.Command => await ExecuteCommandTaskAsync(task, cancellationToken).ConfigureAwait(false),
                ScheduledTaskType.Update => await ExecuteUpdateTaskAsync(task, cancellationToken).ConfigureAwait(false),
                ScheduledTaskType.Backup => await ExecuteBackupTaskAsync(task, cancellationToken).ConfigureAwait(false),
                _ => Result.Failure(Error.InvalidOperation($"Unknown task type: {task.TaskType}"))
            };

            if (result.IsFailure)
            {
                _logger.LogWarning("Scheduled task {TaskName} failed: {Error}", task.Name, result.Error);
            }
            else
            {
                _logger.LogInformation("Scheduled task {TaskName} completed successfully", task.Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing scheduled task {TaskName}", task.Name);
        }
        finally
        {
            // Update last run time and calculate next run
            var nextRun = !string.IsNullOrEmpty(task.CronExpression)
                ? CronValidator.GetNextOccurrence(task.CronExpression, DateTime.UtcNow)
                : null;

            await _taskRepository.UpdateLastRunAsync(task.Id, DateTime.UtcNow, nextRun, CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    public async Task SynchronizeJobsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Synchronizing Hangfire jobs with database...");

        var enabledTasks = await _taskRepository.GetEnabledTasksAsync(cancellationToken).ConfigureAwait(false);

        foreach (var task in enabledTasks)
        {
            if (!string.IsNullOrEmpty(task.CronExpression))
            {
                _jobManager.RegisterRecurringJob(task);
            }
        }

        _logger.LogInformation("Synchronized {Count} scheduled tasks with Hangfire", enabledTasks.Count);
    }

    private async Task<Result> ExecuteCommandTaskAsync(ScheduledTask task, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(task.Configuration))
        {
            return Result.Failure(Error.Validation("Command task has no configuration."));
        }

        CommandTaskConfig? config;
        try
        {
            config = JsonSerializer.Deserialize<CommandTaskConfig>(task.Configuration, JsonOptions);
        }
        catch (JsonException ex)
        {
            return Result.Failure(Error.Validation($"Invalid command configuration: {ex.Message}"));
        }

        if (config is null || string.IsNullOrEmpty(config.Command))
        {
            return Result.Failure(Error.Validation("Command task has no command configured."));
        }

        return await _serverService.SendCommandAsync(task.ServerId, config.Command, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Result> ExecuteUpdateTaskAsync(ScheduledTask task, CancellationToken cancellationToken)
    {
        UpdateTaskConfig? config = null;
        if (!string.IsNullOrEmpty(task.Configuration))
        {
            try
            {
                config = JsonSerializer.Deserialize<UpdateTaskConfig>(task.Configuration, JsonOptions);
            }
            catch (JsonException)
            {
                // Use defaults if configuration is invalid
            }
        }

        // Stop server first if running
        var server = await _serverRepository.GetByIdAsync(task.ServerId, cancellationToken).ConfigureAwait(false);
        var wasRunning = server?.Status == ServerStatus.Running;

        if (wasRunning)
        {
            var stopResult = await _serverService.StopServerAsync(task.ServerId, cancellationToken).ConfigureAwait(false);
            if (stopResult.IsFailure)
            {
                return stopResult;
            }

            // Wait briefly for the server to fully stop
            await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
        }

        var updateResult = await _serverService.UpdateServerAsync(
            task.ServerId,
            config?.Branch,
            config?.BetaPassword,
            null,
            cancellationToken).ConfigureAwait(false);

        if (updateResult.IsFailure)
        {
            return updateResult;
        }

        // Restart if the server was running and configured to restart
        if (wasRunning && (config?.RestartAfterUpdate ?? true))
        {
            return await _serverService.StartServerAsync(task.ServerId, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success();
    }

    private async Task<Result> ExecuteBackupTaskAsync(ScheduledTask task, CancellationToken cancellationToken)
    {
        BackupTaskConfig? config = null;
        if (!string.IsNullOrEmpty(task.Configuration))
        {
            try
            {
                config = JsonSerializer.Deserialize<BackupTaskConfig>(task.Configuration, JsonOptions);
            }
            catch (JsonException)
            {
                // Use defaults if configuration is invalid
            }
        }

        config ??= new BackupTaskConfig();

        return await _backupService.ExecuteScheduledBackupAsync(
            task.ServerId,
            config,
            task.Id,
            cancellationToken).ConfigureAwait(false);
    }

    private static ScheduledTaskDto MapToDto(ScheduledTask task) => new(
        task.Id,
        task.ServerId,
        task.Server.Name,
        task.Name,
        task.TaskType,
        task.CronExpression,
        task.NextRunAt,
        task.LastRunAt,
        task.IsEnabled,
        task.Configuration
    );

    private static ScheduledTaskListItemDto MapToListItemDto(ScheduledTask task) => new(
        task.Id,
        task.ServerId,
        task.Server.Name,
        task.Name,
        task.TaskType,
        task.CronExpression,
        task.NextRunAt,
        task.LastRunAt,
        task.IsEnabled
    );
}
