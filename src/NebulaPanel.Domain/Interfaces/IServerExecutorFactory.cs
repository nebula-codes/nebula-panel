using NebulaPanel.Domain.Enums;

namespace NebulaPanel.Domain.Interfaces;

/// <summary>
/// Factory for creating server executors based on deployment type.
/// </summary>
public interface IServerExecutorFactory
{
    /// <summary>
    /// Gets an executor for the specified deployment type.
    /// </summary>
    IServerExecutor GetExecutor(ServerDeploymentType deploymentType);

    /// <summary>
    /// Gets all registered executors.
    /// </summary>
    IEnumerable<IServerExecutor> GetAllExecutors();
}
