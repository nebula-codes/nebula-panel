namespace NebulaPanel.Domain.Interfaces;

/// <summary>
/// Resolves server paths for Docker-in-Docker scenarios.
/// When the panel runs in a container, local paths (inside the panel container) differ from
/// host paths (what the Docker daemon uses for bind mounts on game server containers).
/// This service auto-detects volume mappings via container self-inspection.
/// </summary>
public interface IServerPathResolver
{
    /// <summary>
    /// Whether the panel is running inside a Docker container.
    /// </summary>
    bool IsDockerEnvironment { get; }

    /// <summary>
    /// Gets the base directory for new server installations.
    /// Returns "/app/servers" in Docker, or the user's home directory natively.
    /// </summary>
    string GetServerBasePath();

    /// <summary>
    /// Translates a local (panel container) path to the corresponding host path
    /// for use in Docker bind mounts. Returns the path unchanged when running natively
    /// or when no matching mount mapping is found.
    /// </summary>
    string ResolveHostPath(string localPath);
}
