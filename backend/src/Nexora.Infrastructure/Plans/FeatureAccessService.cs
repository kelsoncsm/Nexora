using Microsoft.EntityFrameworkCore;
using Nexora.Application.Plans;
using Nexora.Infrastructure.Persistence;

namespace Nexora.Infrastructure.Plans;

public sealed class FeatureAccessService(NexoraDbContext db, ITenantPlanProvider plans) : IFeatureAccessService
{
    public async Task<FeatureAccess> ResolveAsync(Guid tenantId, string featureCode, CancellationToken ct)
    {
        var code = featureCode.Trim().ToUpperInvariant();
        var feature = await db.Features.Where(x => x.Code == code && x.IsActive).Select(x => new { x.Id }).SingleOrDefaultAsync(ct);
        if (feature is null) return new(false, null);
        var tenantOverride = await db.TenantFeatureOverrides.Where(x => x.TenantId == tenantId && x.FeatureId == feature.Id)
            .Select(x => new FeatureAccess(x.Enabled, x.Limit)).SingleOrDefaultAsync(ct);
        if (tenantOverride is not null) return tenantOverride;
        var planId = await plans.GetCurrentPlanIdAsync(tenantId, ct); if (!planId.HasValue) return new(false, null);
        return await db.PlanFeatures.Where(x => x.PlanId == planId && x.FeatureId == feature.Id && x.Plan.IsActive)
            .Select(x => new FeatureAccess(x.Enabled, x.Limit)).SingleOrDefaultAsync(ct) ?? new(false, null);
    }
}

public sealed class UnavailableTenantPlanProvider : ITenantPlanProvider
{ public Task<Guid?> GetCurrentPlanIdAsync(Guid tenantId, CancellationToken cancellationToken) => Task.FromResult<Guid?>(null); }
