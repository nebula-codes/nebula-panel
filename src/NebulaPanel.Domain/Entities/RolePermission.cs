namespace NebulaPanel.Domain.Entities;

/// <summary>
/// Join table for many-to-many relationship between Role and Permission.
/// </summary>
public class RolePermission
{
    public Guid RoleId { get; set; }
    private Role? _role;
    public Role Role
    {
        get => _role ?? throw new InvalidOperationException("Role navigation property was not loaded. Use .Include(x => x.Role) in your query.");
        set => _role = value;
    }

    public Guid PermissionId { get; set; }
    private Permission? _permission;
    public Permission Permission
    {
        get => _permission ?? throw new InvalidOperationException("Permission navigation property was not loaded. Use .Include(x => x.Permission) in your query.");
        set => _permission = value;
    }
}
