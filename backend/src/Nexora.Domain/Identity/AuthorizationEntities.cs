namespace Nexora.Domain.Identity;

public sealed class Role
{
    private Role() { }
    public Role(string name) { Id = Guid.NewGuid(); Name = name; }
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public ICollection<RolePermission> Permissions { get; } = [];
    public ICollection<UserRole> Users { get; } = [];
}

public sealed class Permission
{
    private Permission() { }
    public Permission(string name) { Id = Guid.NewGuid(); Name = name; }
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public ICollection<RolePermission> Roles { get; } = [];
}

public sealed class RolePermission
{
    private RolePermission() { }
    public RolePermission(Guid roleId, Guid permissionId) { RoleId = roleId; PermissionId = permissionId; }
    public Guid RoleId { get; private set; }
    public Role Role { get; private set; } = null!;
    public Guid PermissionId { get; private set; }
    public Permission Permission { get; private set; } = null!;
}

public sealed class UserRole
{
    private UserRole() { }
    public UserRole(Guid userId, Guid roleId) { UserId = userId; RoleId = roleId; }
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public Guid RoleId { get; private set; }
    public Role Role { get; private set; } = null!;
}
