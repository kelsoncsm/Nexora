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
public sealed record AuditView(Guid Id, Guid ActorUserId, Guid? TenantId, string Action, string TargetType, string TargetId,
    bool Succeeded, string CorrelationId, string? Details, DateTimeOffset OccurredAt);

/// <summary>Stable audit action codes (ADR-0021). Never inline these strings in services.</summary>
public static class AuditActions
{
    public const string MemberRoleChanged = "member.role_changed";
    public const string MemberDeactivated = "member.deactivated";
    public const string RolePermissionsChanged = "role.permissions_changed";
    public const string TenantFeatureOverrideConfigured = "tenant_feature_override.configured";
    public const string PlanFeatureConfigured = "plan_feature.configured";
    public const string CheckoutRequested = "billing.checkout_requested";
}

/// <summary>
/// Records an audit entry on the current unit of work (ADR-0021). The row is added to the same
/// DbContext as the business mutation and persisted by the caller's SaveChanges/transaction — so a
/// rolled-back mutation leaves no audit trail. <paramref name="details"/> is serialised to JSON;
/// pass ids and codes only, never secrets or unnecessary PII.
/// </summary>
public interface IAuditLogWriter
{
    void Record(Guid actorUserId, string action, string targetType, string targetId,
        string correlationId, Guid? tenantId = null, object? details = null);
}

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
