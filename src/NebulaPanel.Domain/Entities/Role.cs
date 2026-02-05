namespace NebulaPanel.Domain.Entities;

public class Role
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;        // "Admin", "Moderator", "User"
    public string? Description { get; set; }
    public bool IsSystemRole { get; set; }                  // Cannot be deleted
    public int Priority { get; set; }                       // Higher = more authority

    // Navigation properties
    public ICollection<RolePermission> Permissions { get; set; } = [];
    public ICollection<UserRole> Users { get; set; } = [];
}
