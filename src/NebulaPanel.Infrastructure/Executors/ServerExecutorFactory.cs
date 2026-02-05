using NebulaPanel.Domain.Enums;
using NebulaPanel.Domain.Interfaces;

namespace NebulaPanel.Infrastructure.Executors;

/// <summary>
/// Factory for creating server executors based on deployment type.
/// </summary>
public class ServerExecutorFactory : IServerExecutorFactory
{
    private readonly Dictionary<ServerDeploymentType, IServerExecutor> _executors;

    public ServerExecutorFactory(IEnumerable<IServerExecutor> executors)
    {
        _executors = executors.ToDictionary(e => e.DeploymentType);
    }

    public IServerExecutor GetExecutor(ServerDeploymentType deploymentType)
    {
        if (!_executors.TryGetValue(deploymentType, out var executor))
        {
            throw new NotSupportedException($"No executor registered for deployment type: {deploymentType}");
        }

        return executor;
    }

    public IEnumerable<IServerExecutor> GetAllExecutors() => _executors.Values;
}
