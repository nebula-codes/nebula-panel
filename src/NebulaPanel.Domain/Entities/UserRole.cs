namespace NebulaPanel.Domain.Entities;

/// <summary>
/// Join table for many-to-many relationship between User and Role.
/// </summary>
public class UserRole
{
    public Guid UserId { get; set; }
    private User? _user;
    public User User
    {
        get => _user ?? throw new InvalidOperationException("User navigation property was not loaded. Use .Include(x => x.User) in your query.");
        set => _user = value;
    }

    public Guid RoleId { get; set; }
    private Role? _role;
    public Role Role
    {
        get => _role ?? throw new InvalidOperationException("Role navigation property was not loaded. Use .Include(x => x.Role) in your query.");
        set => _role = value;
    }

    public DateTime AssignedAt { get; set; }
    public Guid? AssignedBy { get; set; }       // User who assigned this role
}
