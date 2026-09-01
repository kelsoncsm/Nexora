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
public sealed record TenantMembership(Guid Id, Guid UserId, string Email, string Role, bool IsActive);
public static class TenantPermissions { public const string CustomersRead="customers.read"; public const string CustomersCreate="customers.create"; public const string CustomersUpdate="customers.update"; public const string CustomersDelete="customers.delete"; public const string ProfessionalsRead="professionals.read";public const string ProfessionalsCreate="professionals.create";public const string ProfessionalsUpdate="professionals.update";public const string ProfessionalsDelete="professionals.delete";public const string ServicesRead="services.read";public const string ServicesCreate="services.create";public const string ServicesUpdate="services.update";public const string ServicesDelete="services.delete";public const string AppointmentsRead="appointments.read";public const string AppointmentsCreate="appointments.create";public const string AppointmentsUpdate="appointments.update";public const string AppointmentsCancel="appointments.cancel";public const string ReportsRead="reports.read";public static readonly string[] CustomerAll=[CustomersRead,CustomersCreate,CustomersUpdate,CustomersDelete];public static readonly string[] All=[..CustomerAll,ProfessionalsRead,ProfessionalsCreate,ProfessionalsUpdate,ProfessionalsDelete,ServicesRead,ServicesCreate,ServicesUpdate,ServicesDelete,AppointmentsRead,AppointmentsCreate,AppointmentsUpdate,AppointmentsCancel,ReportsRead]; }

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
}

public sealed class TenantConflictException(string message) : Exception(message) { }
public sealed class TenantValidationException(string message) : Exception(message) { }
