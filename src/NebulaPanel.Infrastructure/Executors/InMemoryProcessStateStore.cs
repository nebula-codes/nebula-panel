using System.Collections.Concurrent;

namespace NebulaPanel.Infrastructure.Executors;

/// <summary>
/// In-memory store for managed native processes, keyed by server ID.
/// Singleton — shared across the application for the lifetime of the host.
/// Suitable for single-node deployments where all processes are local.
/// </summary>
public sealed class NativeProcessStateStore : IDisposable
{
    private readonly ConcurrentDictionary<Guid, ManagedProcess> _store = new();

    internal ManagedProcess? Get(Guid serverId)
    {
        _store.TryGetValue(serverId, out var state);
        return state;
    }

    internal bool TryGet(Guid serverId, out ManagedProcess? state)
    {
        var found = _store.TryGetValue(serverId, out var value);
        state = value;
        return found;
    }

    internal void Set(Guid serverId, ManagedProcess state)
    {
        if (_store.TryGetValue(serverId, out var existing) && existing != state)
        {
            existing.Dispose();
        }
        _store[serverId] = state;
    }

    internal bool TryRemove(Guid serverId, out ManagedProcess? state)
    {
        var found = _store.TryRemove(serverId, out var value);
        state = value;
        return found;
    }

    internal IReadOnlyCollection<Guid> GetAllServerIds() => _store.Keys.ToList().AsReadOnly();

    public void Dispose()
    {
        foreach (var kvp in _store)
        {
            kvp.Value.Dispose();
        }
        _store.Clear();
    }
}

/// <summary>
/// In-memory store for managed Docker containers, keyed by server ID.
/// Singleton — shared across the application for the lifetime of the host.
/// Suitable for single-node deployments where all containers are local.
/// </summary>
public sealed class DockerContainerStateStore : IDisposable
{
    private readonly ConcurrentDictionary<Guid, ManagedContainer> _store = new();

    internal ManagedContainer? Get(Guid serverId)
    {
        _store.TryGetValue(serverId, out var state);
        return state;
    }

    internal bool TryGet(Guid serverId, out ManagedContainer? state)
    {
        var found = _store.TryGetValue(serverId, out var value);
        state = value;
        return found;
    }

    internal void Set(Guid serverId, ManagedContainer state)
    {
        if (_store.TryGetValue(serverId, out var existing) && existing != state)
        {
            existing.Dispose();
        }
        _store[serverId] = state;
    }

    internal bool TryRemove(Guid serverId, out ManagedContainer? state)
    {
        var found = _store.TryRemove(serverId, out var value);
        state = value;
        return found;
    }

    internal IReadOnlyCollection<Guid> GetAllServerIds() => _store.Keys.ToList().AsReadOnly();

    public void Dispose()
    {
        foreach (var kvp in _store)
        {
            kvp.Value.Dispose();
        }
        _store.Clear();
    }
}
