namespace Nexora.Application.Administration;

public static class PlatformPermissions
{
    public const string Access = "platform.access";
    public const string TenantsManage = "platform.tenants.manage";
    public const string UsersRead = "platform.users.read";
    public const string SegmentsManage = "platform.segments.manage";
    public const string AuditRead = "platform.audit.read";
    public static readonly string[] All = [Access, TenantsManage, UsersRead, SegmentsManage, AuditRead];
}

public sealed record AdminDashboard(int TotalTenants, int ActiveTenants, int TotalUsers, int ActiveSegments);
public sealed record AdminTenant(Guid Id, string Name, string Slug, bool IsActive, DateTimeOffset CreatedAt);
public sealed record AdminUser(Guid Id, string Email, bool IsActive, DateTimeOffset CreatedAt);
public sealed record SegmentView(Guid Id, string Code, string Name, bool IsActive, DateTimeOffset CreatedAt);
public sealed record AuditView(Guid Id, Guid ActorUserId, string Action, string TargetType, string TargetId,
    bool Succeeded, string CorrelationId, DateTimeOffset OccurredAt);

public interface IAdministrationService
{
    Task<AdminDashboard> DashboardAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminTenant>> GetTenantsAsync(CancellationToken cancellationToken);
    Task<AdminTenant?> GetTenantAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> SetTenantActiveAsync(Guid actorId, Guid id, bool active, string correlationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AdminUser>> GetUsersAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<SegmentView>> GetSegmentsAsync(CancellationToken cancellationToken);
    Task<SegmentView> CreateSegmentAsync(Guid actorId, string code, string name, string correlationId, CancellationToken cancellationToken);
    Task<SegmentView?> UpdateSegmentAsync(Guid actorId, Guid id, string name, bool active, string correlationId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AuditView>> GetAuditAsync(CancellationToken cancellationToken);
}

public interface IPlatformAdminBootstrapper
{
    Task BootstrapAsync(CancellationToken cancellationToken);
}

public sealed class AdministrationValidationException(string message) : Exception(message);
public sealed class AdministrationConflictException(string message) : Exception(message);
