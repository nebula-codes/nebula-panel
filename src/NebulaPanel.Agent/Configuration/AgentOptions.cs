namespace NebulaPanel.Agent.Configuration;

/// <summary>
/// Configuration for the Nebula Panel Agent.
/// </summary>
public class AgentOptions
{
    public const string Section = "Agent";

    /// <summary>
    /// The unique node identifier assigned by the manager.
    /// </summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>
    /// Bearer token for authenticating gRPC calls from the manager.
    /// Must match the AgentToken stored on the Node entity.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Port the gRPC server listens on.
    /// </summary>
    public int Port { get; set; } = 5148;

    /// <summary>
    /// Optional TLS certificate path for HTTPS.
    /// </summary>
    public string? TlsCertPath { get; set; }

    /// <summary>
    /// Optional TLS certificate key path.
    /// </summary>
    public string? TlsKeyPath { get; set; }
}
