namespace NebulaPanel.Domain.Entities;

public class Permission
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;        // "servers.create", "servers.*.start"
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;    // "Servers", "Users", "System"
    public string? Description { get; set; }

    // Navigation properties
    public ICollection<RolePermission> Roles { get; set; } = [];
}
