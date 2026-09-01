namespace Nexora.Domain.Tenancy;

public sealed class Tenant
{
    private Tenant() { }
    public Tenant(string name, string slug, string timeZoneId, DateTimeOffset createdAt, Guid? segmentId = null)
    { Id = Guid.NewGuid(); Name = name; Slug = slug; TimeZoneId = timeZoneId; SegmentId = segmentId; IsActive = true; CreatedAt = createdAt; }
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string TimeZoneId { get; private set; } = string.Empty;
    public Guid? SegmentId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public ICollection<TenantUser> Users { get; } = [];
    public ICollection<TenantRole> Roles { get; } = [];
    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
    /// <summary>Updates the tenant-editable profile fields. Slug and ownership are immutable here.</summary>
    public void UpdateProfile(string name, string timeZoneId)
    {
        Name = name;
        TimeZoneId = timeZoneId;
    }
    public void AddInitialAdministrator(Guid userId, IEnumerable<string> permissions, DateTimeOffset now)
    {
        var role = new TenantRole(Id, "ADMIN", "Tenant administrator", true);
        foreach (var permission in permissions.Distinct(StringComparer.Ordinal))
            role.Permissions.Add(new TenantRolePermission(role.Id, permission));
        Roles.Add(role);
        Users.Add(new TenantUser(Id, userId, role.Id, now));
    }
}
