namespace Nexora.Application.Plans;

public sealed record FeatureAccess(bool Enabled, int? Limit);
public interface ITenantPlanProvider { Task<Guid?> GetCurrentPlanIdAsync(Guid tenantId, CancellationToken cancellationToken); }
public interface IFeatureAccessService { Task<FeatureAccess> ResolveAsync(Guid tenantId, string featureCode, CancellationToken cancellationToken); }

/// <summary>
/// The feature catalog the runtime enforces (ADR-0019). Each code maps to one module endpoint group;
/// the group is gated on the code, and the seed migration creates the rows. Adding or removing a code
/// here is an architectural change, not configuration.
/// </summary>
public static class FeatureCodes
{
    public const string Customers = "CUSTOMERS";
    public const string Scheduling = "SCHEDULING";
    public const string Professionals = "PROFESSIONALS";
    public const string Services = "SERVICES";
    public const string Reports = "REPORTS";

    /// <summary>Every gated module code, in a stable order.</summary>
    public static readonly string[] All = [Customers, Scheduling, Professionals, Services, Reports];
}

/// <summary>The current plan (or an explicit override) does not include the requested module. Maps to 403.</summary>
public sealed class FeatureNotInPlanException(string featureCode)
    : Exception($"The '{featureCode}' module is not available in the current plan.")
{
    public string FeatureCode { get; } = featureCode;
}

/// <summary>A plan limit for the feature has been reached. Maps to 409.</summary>
public sealed class PlanLimitExceededException(string featureCode, int limit, int current)
    : Exception($"The plan limit for '{featureCode}' ({limit}) has been reached.")
{
    public string FeatureCode { get; } = featureCode;
    public int Limit { get; } = limit;
    public int Current { get; } = current;
}
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
