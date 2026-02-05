namespace NebulaPanel.Domain.Entities;

/// <summary>
/// Per-server permission override for a user.
/// Allows granting or denying specific permissions on individual servers.
/// </summary>
public class ServerPermission
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    private User? _user;
    public User User
    {
        get => _user ?? throw new InvalidOperationException("User navigation property was not loaded. Use .Include(x => x.User) in your query.");
        set => _user = value;
    }

    public Guid ServerId { get; set; }
    private GameServer? _server;
    public GameServer Server
    {
        get => _server ?? throw new InvalidOperationException("Server navigation property was not loaded. Use .Include(x => x.Server) in your query.");
        set => _server = value;
    }

    public string PermissionCode { get; set; } = string.Empty;  // "start", "stop", "console", "files", "config"
    public bool IsGranted { get; set; }                         // true = grant, false = deny
}
