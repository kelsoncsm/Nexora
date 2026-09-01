using Nexora.Domain.Identity;

namespace Nexora.Domain.Tenancy;

public sealed class TenantUser
{
    private TenantUser() { }
    public TenantUser(Guid tenantId, Guid userId, Guid tenantRoleId, DateTimeOffset createdAt)
    { Id = Guid.NewGuid(); TenantId = tenantId; UserId = userId; TenantRoleId = tenantRoleId; IsActive = true; CreatedAt = createdAt; }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Tenant Tenant { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public Guid TenantRoleId { get; private set; }
    public TenantRole TenantRole { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public void Deactivate() => IsActive = false;
    public void AssignRole(Guid tenantRoleId) => TenantRoleId = tenantRoleId;
}

public sealed class TenantRole
{
    private TenantRole() { }
    public TenantRole(Guid tenantId,string name,string description,bool isSystem)
    { Id=Guid.NewGuid();TenantId=tenantId;Name=name;Description=description;IsSystem=isSystem; }
    public Guid Id{get;private set;} public Guid TenantId{get;private set;} public Tenant Tenant{get;private set;}=null!;
    public string Name{get;private set;}=string.Empty; public string Description{get;private set;}=string.Empty; public bool IsSystem{get;private set;}
    public ICollection<TenantRolePermission> Permissions{get;}=[]; public ICollection<TenantUser> Users{get;}=[];
}

public sealed class TenantRolePermission
{
    private TenantRolePermission() { }
    public TenantRolePermission(Guid tenantRoleId,string permissionKey){TenantRoleId=tenantRoleId;PermissionKey=permissionKey;}
    public Guid TenantRoleId{get;private set;} public TenantRole TenantRole{get;private set;}=null!; public string PermissionKey{get;private set;}=string.Empty;
}
