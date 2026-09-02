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
public sealed record TenantProfile(Guid Id, string Name, string Slug, string TimeZoneId, bool IsActive, DateTimeOffset CreatedAt);
public sealed record TenantRoleView(Guid Id, string Name, string Description, bool IsSystem, int MemberCount, IReadOnlyCollection<string> Permissions);
public sealed record TenantPermissionDescriptor(string Key, string Module, string Action);
public static class TenantPermissions { public const string CustomersRead="customers.read"; public const string CustomersCreate="customers.create"; public const string CustomersUpdate="customers.update"; public const string CustomersDelete="customers.delete"; public const string ProfessionalsRead="professionals.read";public const string ProfessionalsCreate="professionals.create";public const string ProfessionalsUpdate="professionals.update";public const string ProfessionalsDelete="professionals.delete";public const string ServicesRead="services.read";public const string ServicesCreate="services.create";public const string ServicesUpdate="services.update";public const string ServicesDelete="services.delete";public const string AppointmentsRead="appointments.read";public const string AppointmentsCreate="appointments.create";public const string AppointmentsUpdate="appointments.update";public const string AppointmentsCancel="appointments.cancel";public const string ReportsRead="reports.read";public const string TenantManage="tenant.manage";public static readonly string[] CustomerAll=[CustomersRead,CustomersCreate,CustomersUpdate,CustomersDelete];public static readonly string[] All=[..CustomerAll,ProfessionalsRead,ProfessionalsCreate,ProfessionalsUpdate,ProfessionalsDelete,ServicesRead,ServicesCreate,ServicesUpdate,ServicesDelete,AppointmentsRead,AppointmentsCreate,AppointmentsUpdate,AppointmentsCancel,ReportsRead,TenantManage];
    /// <summary>The assignable permission catalog, split into module/action for grouped UIs.</summary>
    public static IReadOnlyList<TenantPermissionDescriptor> Catalog { get; } =
        All.Select(key => { var parts = key.Split('.', 2); return new TenantPermissionDescriptor(key, parts[0], parts.Length > 1 ? parts[1] : string.Empty); }).ToArray();
}

public interface ITenancyService
{
    Task<PublicTenant?> ResolvePublicAsync(string slug, CancellationToken cancellationToken);
    Task<PublicTenant> CreateAsync(Guid userId, string name, string slug, string timeZoneId, CancellationToken cancellationToken);
    Task<bool> ValidateMembershipAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<string>> GetPermissionsAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);
    Task<Guid?> ResolveMembershipTenantAsync(Guid userId, string slug, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<TenantMembership>> GetMembersAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<bool> DeactivateMembershipAsync(Guid tenantId, Guid actorUserId, Guid membershipId, CancellationToken cancellationToken);
    Task<bool> AssignRoleAsync(Guid tenantId,Guid membershipId,Guid roleId,CancellationToken cancellationToken);

    Task<TenantProfile?> GetProfileAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<TenantProfile> UpdateProfileAsync(Guid tenantId, string name, string timeZoneId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<TenantRoleView>> GetRolesAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<TenantRoleView?> GetRoleAsync(Guid tenantId, Guid roleId, CancellationToken cancellationToken);
    Task<TenantRoleView> CreateRoleAsync(Guid tenantId, string name, string description, IReadOnlyCollection<string> permissions, CancellationToken cancellationToken);
    Task<TenantRoleView?> UpdateRoleAsync(Guid tenantId, Guid roleId, string name, string description, CancellationToken cancellationToken);
    Task<TenantRoleView?> SetRolePermissionsAsync(Guid tenantId, Guid roleId, IReadOnlyCollection<string> permissions, CancellationToken cancellationToken);
}

public sealed class TenantConflictException(string message) : Exception(message) { }
public sealed class TenantValidationException(string message) : Exception(message) { }
