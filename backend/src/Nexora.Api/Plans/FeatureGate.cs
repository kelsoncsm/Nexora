using Nexora.Application.Plans;
using Nexora.Application.Tenancy;

namespace Nexora.Api.Plans;

/// <summary>
/// Server-side module enforcement (ADR-0019). <see cref="RequireFeature{TBuilder}"/> attaches an
/// endpoint filter that resolves the tenant's access to a feature via <see cref="IFeatureAccessService"/>
/// and rejects with 403 <c>feature_not_in_plan</c> when the plan (or an override) does not include it.
/// It runs after authentication, the tenant-context middleware and authorization, so a request that
/// reaches the filter is already an authorized member of an initialized tenant.
/// </summary>
public static class FeatureGate
{
    public static TBuilder RequireFeature<TBuilder>(this TBuilder builder, string featureCode)
        where TBuilder : IEndpointConventionBuilder =>
        builder.AddEndpointFilter(new FeatureGateFilter(featureCode));

    private sealed class FeatureGateFilter(string featureCode) : IEndpointFilter
    {
        public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            var http = context.HttpContext;
            var tenant = http.RequestServices.GetRequiredService<ITenantContext>();
            // No tenant context => the endpoint's authorization policy (which requires the tenant_id
            // claim) already rejected the request; nothing for the feature gate to add.
            if (!tenant.IsAvailable) return await next(context);

            var cacheKey = $"feature:{featureCode}";
            if (http.Items[cacheKey] is not FeatureAccess access)
            {
                access = await http.RequestServices.GetRequiredService<IFeatureAccessService>()
                    .ResolveAsync(tenant.TenantId, featureCode, http.RequestAborted);
                http.Items[cacheKey] = access;
            }

            if (!access.Enabled) throw new FeatureNotInPlanException(featureCode);
            return await next(context);
        }
    }
}
