namespace Nexora.Application.Tenancy;

public interface ITenantContext
{
    bool IsAvailable { get; }
    Guid TenantId { get; }
    Guid UserId { get; }
}

public interface ITenantContextInitializer
{
    void Initialize(Guid tenantId, Guid userId);
}

public sealed record PublicTenant(Guid Id, string Name, string Slug, string TimeZoneId);
public sealed record TenantMembership(Guid Id, Guid UserId, string Email, string Role, Guid RoleId, bool IsActive);
/// <summary>An active company the authenticated user belongs to, used to re-enter a tenant after login.</summary>
public sealed record UserTenant(Guid Id, string Name, string Slug, string RoleName);
public sealed record TenantProfile(Guid Id, string Name, string Slug, string TimeZoneId, bool IsActive, DateTimeOffset CreatedAt);
public sealed record TenantRoleView(Guid Id, string Name, string Description, bool IsSystem, int MemberCount, IReadOnlyCollection<string> Permissions);
public sealed record TenantPermissionDescriptor(string Key, string Module, string Action);
public static class TenantPermissions
{
    public const string CustomersRead = "customers.read";
    public const string CustomersCreate = "customers.create";
    public const string CustomersUpdate = "customers.update";
    public const string CustomersDelete = "customers.delete";
    public const string ProfessionalsRead = "professionals.read";
    public const string ProfessionalsCreate = "professionals.create";
    public const string ProfessionalsUpdate = "professionals.update";
    public const string ProfessionalsDelete = "professionals.delete";
    public const string ServicesRead = "services.read";
    public const string ServicesCreate = "services.create";
    public const string ServicesUpdate = "services.update";
    public const string ServicesDelete = "services.delete";
    public const string AppointmentsRead = "appointments.read";
    public const string AppointmentsCreate = "appointments.create";
    public const string AppointmentsUpdate = "appointments.update";
    public const string AppointmentsCancel = "appointments.cancel";
    public const string ReportsRead = "reports.read";

    /// <summary>
    /// Coarse gate for the tenant-administration surface still without CRUD granularity:
    /// company profile, roles, permissions and billing. Splitting this into
    /// <c>tenant.profile.*</c> / <c>tenant.roles.*</c> / <c>tenant.billing.*</c> is a tracked
    /// architectural gap (see <c>docs/NEXORA-UI-COVERAGE.md §7</c>).
    /// </summary>
    public const string TenantManage = "tenant.manage";

    // Member management has its own CRUD granularity (approved 2026-09-02). Roles that hold
    // tenant.manage are backfilled with all four in migration 20260902150000.
    public const string MembersRead = "tenant.members.read";
    public const string MembersCreate = "tenant.members.create";
    public const string MembersUpdate = "tenant.members.update";
    public const string MembersDelete = "tenant.members.delete";

    public static readonly string[] CustomerAll = [CustomersRead, CustomersCreate, CustomersUpdate, CustomersDelete];
    public static readonly string[] MemberAll = [MembersRead, MembersCreate, MembersUpdate, MembersDelete];
    public static readonly string[] All =
    [
        .. CustomerAll,
        ProfessionalsRead, ProfessionalsCreate, ProfessionalsUpdate, ProfessionalsDelete,
        ServicesRead, ServicesCreate, ServicesUpdate, ServicesDelete,
        AppointmentsRead, AppointmentsCreate, AppointmentsUpdate, AppointmentsCancel,
        ReportsRead, TenantManage,
        .. MemberAll,
    ];

    /// <summary>
    /// The assignable permission catalog, split into module/action for grouped UIs. Two-segment
    /// keys (<c>customers.read</c>) split as module/action; three-segment keys
    /// (<c>tenant.members.read</c>) group the first two segments as the module.
    /// </summary>
    public static IReadOnlyList<TenantPermissionDescriptor> Catalog { get; } =
        All.Select(key =>
        {
            var parts = key.Split('.');
            return parts.Length >= 3
                ? new TenantPermissionDescriptor(key, $"{parts[0]}.{parts[1]}", string.Join('.', parts[2..]))
                : new TenantPermissionDescriptor(key, parts[0], parts.Length > 1 ? parts[1] : string.Empty);
        }).ToArray();
}

public interface ITenancyService
{
    Task<PublicTenant?> ResolvePublicAsync(string slug, CancellationToken cancellationToken);
    Task<PublicTenant> CreateAsync(Guid userId, string name, string slug, string timeZoneId, CancellationToken cancellationToken);
    Task<bool> ValidateMembershipAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<string>> GetPermissionsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<UserTenant>> GetUserTenantsAsync(Guid userId, CancellationToken cancellationToken);
    Task<Guid?> ResolveMembershipTenantAsync(Guid userId, string slug, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<TenantMembership>> GetMembersAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<bool> DeactivateMembershipAsync(Guid tenantId, Guid actorUserId, Guid membershipId, string correlationId, CancellationToken cancellationToken);
    Task<bool> AssignRoleAsync(Guid tenantId, Guid actorUserId, Guid membershipId, Guid roleId, string correlationId, CancellationToken cancellationToken);

    Task<TenantProfile?> GetProfileAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<TenantProfile> UpdateProfileAsync(Guid tenantId, string name, string timeZoneId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TenantRoleView>> GetRolesAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<TenantRoleView?> GetRoleAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken);
    Task<TenantRoleView> CreateRoleAsync(Guid tenantId, string name, string description, IReadOnlyCollection<string> permissions, CancellationToken cancellationToken);
    Task<TenantRoleView?> UpdateRoleAsync(Guid tenantId, Guid roleId, string name, string description, CancellationToken cancellationToken);
    Task<TenantRoleView?> SetRolePermissionsAsync(Guid tenantId, Guid actorUserId, Guid roleId, IReadOnlyCollection<string> permissions, string correlationId, CancellationToken cancellationToken);
}

public sealed class TenantConflictException(string message) : Exception(message) { }
public sealed class TenantValidationException(string message) : Exception(message) { }

/// <summary>
/// The caller is authenticated and holds the endpoint's permission, but the specific operation would
/// exceed their authority — e.g. assigning a role that grants permissions they do not hold. Maps to
/// HTTP 403.
/// </summary>
public sealed class TenantForbiddenException(string message) : Exception(message) { }

public static class RoleGrant
{
    /// <summary>
    /// No-privilege-escalation rule (architectural decision 2026-09-02): a role may be assigned to a
    /// member, used in an invitation, or have its permission set edited only when the resulting
    /// permission set is a subset of the actor's own effective permission set —
    /// <c>RolePermissions ⊆ ActorPermissions</c>. There is no exception for <c>tenant.manage</c> or
    /// for system roles: holding <c>tenant.manage</c> does not grant authority to delegate
    /// permissions the actor does not personally hold.
    /// </summary>
    public static bool IsWithinActorAuthority(IEnumerable<string> rolePermissions, IEnumerable<string> actorPermissions)
    {
        var actor = actorPermissions as ISet<string> ?? actorPermissions.ToHashSet(StringComparer.Ordinal);
        return rolePermissions.All(actor.Contains);
    }
}
