using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Domain.Billing;
using Nexora.Domain.Plans;
using Nexora.Infrastructure.Persistence;

namespace Nexora.IntegrationTests;

/// <summary>
/// Test helper for the P1.1 feature-enforcement layer (ADR-0019). The seed migration that creates
/// the Feature catalog does not run on the in-memory provider, and a tenant only has a module when a
/// plan (or override) grants it — so any test that exercises a gated module must set this up.
/// </summary>
public static class TestFeatureCatalog
{
    /// <summary>Mirror of <c>Nexora.Application.Plans.FeatureCodes.All</c> — the gated module codes.</summary>
    public static readonly IReadOnlyList<string> ModuleCodes =
        ["CUSTOMERS", "SCHEDULING", "PROFESSIONALS", "SERVICES", "REPORTS"];

    /// <summary>Ensures the module Feature rows exist. Idempotent (safe against the migrated PG database).</summary>
    public static async Task SeedFeaturesAsync(NexoraDbContext db, CancellationToken ct = default)
    {
        var existing = await db.Features.Select(x => x.Code).ToListAsync(ct);
        var now = DateTimeOffset.UtcNow;
        foreach (var code in ModuleCodes.Where(code => !existing.Contains(code)))
            db.Features.Add(new Feature(code, code, now));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Seeds the Feature rows and returns a plan that enables every module with the given per-code limits.</summary>
    public static async Task<Plan> CreateAllModulesPlanAsync(
        NexoraDbContext db, IReadOnlyDictionary<string, int?>? limits = null, CancellationToken ct = default)
    {
        await SeedFeaturesAsync(db, ct);
        var features = await db.Features.Where(x => ModuleCodes.Contains(x.Code)).ToListAsync(ct);
        var plan = new Plan($"TEST_ALL_{Guid.NewGuid():N}", "Test — all modules", DateTimeOffset.UtcNow);
        foreach (var feature in features)
            plan.Features.Add(new PlanFeature(plan.Id, feature.Id, true,
                limits is not null && limits.TryGetValue(feature.Code, out var limit) ? limit : null));
        db.Plans.Add(plan);
        await db.SaveChangesAsync(ct);
        return plan;
    }

    /// <summary>
    /// In-memory <see cref="ApiFactory"/> (stub plan provider): seed features + an all-modules plan
    /// and point the stub at it, so every tenant in the factory has every module.
    /// </summary>
    public static async Task GrantAllModulesAsync(ApiFactory factory, IReadOnlyDictionary<string, int?>? limits = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        var plan = await CreateAllModulesPlanAsync(db, limits);
        factory.PlanProvider.PlanId = plan.Id;
    }

    /// <summary>
    /// Persistent provider / Postgres: seed features + an all-modules plan + a Trialing subscription
    /// for <paramref name="tenantId"/>, so the real plan provider resolves the modules.
    /// </summary>
    public static async Task<Guid> GrantAllModulesToTenantAsync(
        WebApplicationFactory<Program> factory, Guid tenantId, IReadOnlyDictionary<string, int?>? limits = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        var plan = await CreateAllModulesPlanAsync(db, limits);
        db.Subscriptions.Add(new Subscription(tenantId, plan.Id, BillingInterval.Monthly, DateTimeOffset.UtcNow, TimeSpan.FromDays(14)));
        await db.SaveChangesAsync();
        return plan.Id;
    }
}
