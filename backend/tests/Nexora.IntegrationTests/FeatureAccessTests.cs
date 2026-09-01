using Microsoft.Extensions.DependencyInjection;
using Nexora.Application.Plans;
using Nexora.Domain.Plans;
using Nexora.Domain.Tenancy;
using Nexora.Infrastructure.Persistence;

namespace Nexora.IntegrationTests;

public sealed class FeatureAccessTests
{
    [Fact]
    public async Task ResolvesPlanLimitOverrideDisabledAndMissingFeature()
    {
        await using var factory=new ApiFactory(); _=factory.CreateClient();
        await using var scope=factory.Services.CreateAsyncScope(); var db=scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        var tenant=new Tenant("Tenant","tenant","UTC",DateTimeOffset.UtcNow);var feature=new Feature("REPORTS","Reports",DateTimeOffset.UtcNow);var plan=new Plan("PRO","Pro",DateTimeOffset.UtcNow);
        plan.Features.Add(new PlanFeature(plan.Id,feature.Id,true,25));db.AddRange(tenant,feature,plan);await db.SaveChangesAsync();factory.PlanProvider.PlanId=plan.Id;
        var resolver=scope.ServiceProvider.GetRequiredService<IFeatureAccessService>();
        Assert.Equal(new FeatureAccess(true,25),await resolver.ResolveAsync(tenant.Id,"reports",CancellationToken.None));
        db.TenantFeatureOverrides.Add(new TenantFeatureOverride(tenant.Id,feature.Id,false,0));await db.SaveChangesAsync();
        Assert.Equal(new FeatureAccess(false,0),await resolver.ResolveAsync(tenant.Id,"REPORTS",CancellationToken.None));
        Assert.Equal(new FeatureAccess(false,null),await resolver.ResolveAsync(tenant.Id,"MISSING",CancellationToken.None));
    }
}
