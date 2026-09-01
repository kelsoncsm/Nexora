namespace Nexora.Application.Plans;

public sealed record FeatureAccess(bool Enabled, int? Limit);
public interface ITenantPlanProvider { Task<Guid?> GetCurrentPlanIdAsync(Guid tenantId, CancellationToken cancellationToken); }
public interface IFeatureAccessService { Task<FeatureAccess> ResolveAsync(Guid tenantId, string featureCode, CancellationToken cancellationToken); }
public sealed record FeatureView(Guid Id, string Code, string Name, bool IsActive);
public sealed record PlanFeatureView(Guid FeatureId, string FeatureCode, bool Enabled, int? Limit);
public sealed record PlanView(Guid Id, string Code, string Name, bool IsActive, bool IsPublic, bool IsTrialEligible, IReadOnlyList<PlanFeatureView> Features);
public sealed record OverrideView(Guid Id, Guid TenantId, Guid FeatureId, string FeatureCode, bool Enabled, int? Limit);
public interface IPlanCatalogService
{
    Task<IReadOnlyList<FeatureView>> GetFeaturesAsync(CancellationToken cancellationToken);
    Task<FeatureView> CreateFeatureAsync(string code, string name, CancellationToken cancellationToken);
    Task<FeatureView?> UpdateFeatureAsync(Guid id, string name, bool active, CancellationToken cancellationToken);
    Task<IReadOnlyList<PlanView>> GetPlansAsync(CancellationToken cancellationToken);
    Task<PlanView> CreatePlanAsync(string code, string name, CancellationToken cancellationToken);
    Task<PlanView?> UpdatePlanAsync(Guid id, string name, bool active, bool isPublic, bool isTrialEligible, CancellationToken cancellationToken);
    Task<PlanFeatureView?> ConfigurePlanFeatureAsync(Guid planId, Guid featureId, bool enabled, int? limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<OverrideView>> GetOverridesAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<OverrideView?> ConfigureOverrideAsync(Guid tenantId, Guid featureId, bool enabled, int? limit, CancellationToken cancellationToken);
}
public sealed class PlanCatalogValidationException(string message) : Exception(message);
public sealed class PlanCatalogConflictException(string message) : Exception(message);
