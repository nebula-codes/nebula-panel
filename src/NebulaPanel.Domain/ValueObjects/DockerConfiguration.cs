namespace NebulaPanel.Domain.ValueObjects;

public class DockerConfiguration
{
    public string Image { get; set; } = string.Empty;
    public string Tag { get; set; } = "latest";
    public Dictionary<string, string> EnvironmentVariables { get; set; } = [];
    public List<VolumeMount> Volumes { get; set; } = [];
    public List<PortMapping> Ports { get; set; } = [];
    public string? Network { get; set; }
    public ResourceLimits? Limits { get; set; }
    public RestartPolicy RestartPolicy { get; set; }

    /// <summary>
    /// Working directory inside the container.
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Command to run in the container (overrides image CMD).
    /// </summary>
    public string? Command { get; set; }

    /// <summary>
    /// Entrypoint for the container (overrides image ENTRYPOINT).
    /// </summary>
    public string? Entrypoint { get; set; }

    /// <summary>
    /// Whether to allocate a pseudo-TTY for the container.
    /// Required for some servers that need TTY for stdin command processing.
    /// </summary>
    public bool Tty { get; set; }

    /// <summary>
    /// Whether to run the container as root (or the image's default user) instead of the host user.
    /// Some images like jacobsmile/tmodloader1.4 require root to function properly.
    /// </summary>
    public bool RunAsRoot { get; set; }

    /// <summary>
    /// How to send commands to the running server process inside the container.
    /// Default is Stdin (write to the container's stdin). Some images (e.g. tModLoader)
    /// run the server inside tmux, requiring commands to be sent via exec inject.
    /// </summary>
    public DockerCommandMode CommandMode { get; set; } = DockerCommandMode.Stdin;
}

/// <summary>
/// How commands are delivered to the game server process inside a Docker container.
/// </summary>
public enum DockerCommandMode
{
    /// <summary>
    /// Write to the container's stdin (default). Works when the server process is PID 1
    /// or otherwise reads from stdin directly.
    /// </summary>
    Stdin,

    /// <summary>
    /// Use <c>docker exec &lt;container&gt; inject "command"</c> to send commands via tmux.
    /// Required for images like jacobsmile/tmodloader1.4 that run the server inside tmux.
    /// </summary>
    TmuxInject
}

public class VolumeMount
{
    /// <summary>
    /// Host path for bind mounts, or volume name for named volumes.
    /// </summary>
    public string HostPath { get; set; } = string.Empty;

    public string ContainerPath { get; set; } = string.Empty;
    public bool ReadOnly { get; set; }

    /// <summary>
    /// If true, HostPath is treated as a Docker named volume instead of a host path.
    /// Named volumes are stored within Docker and have better I/O performance on Windows.
    /// </summary>
    public bool IsNamedVolume { get; set; }
}

public class PortMapping
{
    public int HostPort { get; set; }
    public int ContainerPort { get; set; }
    public string Protocol { get; set; } = "tcp";   // tcp, udp
}

public enum RestartPolicy
{
    None,
    Always,
    OnFailure,
    UnlessStopped
}
