using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Domain.Billing;
using Nexora.Domain.Plans;
using Nexora.Infrastructure.Persistence;

namespace Nexora.IntegrationTests;

/// <summary>
/// P1.1 / ADR-0019 — module endpoint groups are gated on the tenant's effective plan feature.
/// A plan (or override) that does not include a module answers 403 <c>feature_not_in_plan</c>;
/// a reached plan limit answers 409 <c>plan_limit_reached</c>. Identity, tenant administration,
/// onboarding and billing are never gated. Uses the persistent plan provider + real Subscription,
/// exercising the same resolution path production uses.
/// </summary>
public sealed class FeatureEnforcementTests
{
    private static readonly string[] AllModules = ["CUSTOMERS", "SCHEDULING", "PROFESSIONALS", "SERVICES", "REPORTS"];
    private static readonly string[] EveryModuleButReports = ["CUSTOMERS", "SCHEDULING", "PROFESSIONALS", "SERVICES"];
    private static readonly string[] CustomersReadOnly = ["customers.read"];

    [Fact]
    public async Task PlanWithoutAModuleBlocksItWith403AndKeepsTheGrantedOnes()
    {
        await using var factory = ApiFactory.WithPersistentPlanProvider();
        using var client = factory.CreateClient();
        var (global, slug, tenantId) = await SetUpTenantAsync(client, "fe-partial@nexora.test", "fe-partial");
        await ConfigurePlanAsync(factory, tenantId, EveryModuleButReports);
        client.DefaultRequestHeaders.Authorization = await ScopedAsync(client, global, slug);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/customers")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/professionals")).StatusCode);

        var blocked = await client.GetAsync($"/api/v1/reports/overview?from={From()}&to={To()}");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        var problem = await blocked.Content.ReadFromJsonAsync<Problem>();
        Assert.Equal("feature_not_in_plan", problem!.Code);
        Assert.Equal("REPORTS", problem.Feature);
    }

    [Fact]
    public async Task TenantOverrideEnablesAModuleThePlanOmitsImmediately()
    {
        await using var factory = ApiFactory.WithPersistentPlanProvider();
        using var client = factory.CreateClient();
        var (global, slug, tenantId) = await SetUpTenantAsync(client, "fe-override-on@nexora.test", "fe-override-on");
        await ConfigurePlanAsync(factory, tenantId, EveryModuleButReports);
        await SetOverrideAsync(factory, tenantId, "REPORTS", enabled: true);
        client.DefaultRequestHeaders.Authorization = await ScopedAsync(client, global, slug);

        // Same session, no token refresh — the endpoint filter resolves access per request.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/reports/overview?from={From()}&to={To()}")).StatusCode);
    }

    [Fact]
    public async Task TenantOverrideDisablesAModuleThePlanGrants()
    {
        await using var factory = ApiFactory.WithPersistentPlanProvider();
        using var client = factory.CreateClient();
        var (global, slug, tenantId) = await SetUpTenantAsync(client, "fe-override-off@nexora.test", "fe-override-off");
        await ConfigurePlanAsync(factory, tenantId, AllModules);
        await SetOverrideAsync(factory, tenantId, "CUSTOMERS", enabled: false);
        client.DefaultRequestHeaders.Authorization = await ScopedAsync(client, global, slug);

        var blocked = await client.GetAsync("/api/v1/customers");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal("feature_not_in_plan", (await blocked.Content.ReadFromJsonAsync<Problem>())!.Code);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/services")).StatusCode);
    }

    [Fact]
    public async Task TenantWithoutASubscriptionIsBlockedFromEveryModule()
    {
        await using var factory = ApiFactory.WithPersistentPlanProvider();
        using var client = factory.CreateClient();
        var (global, slug, _) = await SetUpTenantAsync(client, "fe-nosub@nexora.test", "fe-nosub");
        client.DefaultRequestHeaders.Authorization = await ScopedAsync(client, global, slug);

        foreach (var path in new[]
        {
            "/api/v1/customers", "/api/v1/professionals", "/api/v1/services",
            "/api/v1/appointments?from=2026-09-15T00:00:00Z&to=2026-09-16T00:00:00Z",
            $"/api/v1/reports/overview?from={From()}&to={To()}"
        })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task PlanLimitIsEnforcedOnCreationWith409()
    {
        await using var factory = ApiFactory.WithPersistentPlanProvider();
        using var client = factory.CreateClient();
        var (global, slug, tenantId) = await SetUpTenantAsync(client, "fe-limit@nexora.test", "fe-limit");
        await ConfigurePlanAsync(factory, tenantId, AllModules, new Dictionary<string, int?> { ["PROFESSIONALS"] = 2 });
        client.DefaultRequestHeaders.Authorization = await ScopedAsync(client, global, slug);

        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/professionals", new { name = "Ana", isActive = true })).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/professionals", new { name = "Bruno", isActive = true })).StatusCode);

        var overLimit = await client.PostAsJsonAsync("/api/v1/professionals", new { name = "Carla", isActive = true });
        Assert.Equal(HttpStatusCode.Conflict, overLimit.StatusCode);
        var problem = await overLimit.Content.ReadFromJsonAsync<Problem>();
        Assert.Equal("plan_limit_reached", problem!.Code);
        Assert.Equal("PROFESSIONALS", problem.Feature);
        Assert.Equal(2, problem.Limit);
    }

    [Fact]
    public async Task IdentityTenantAdministrationAndOnboardingAreNeverGated()
    {
        await using var factory = ApiFactory.WithPersistentPlanProvider();
        using var client = factory.CreateClient();
        var (global, slug, _) = await SetUpTenantAsync(client, "fe-ungated@nexora.test", "fe-ungated");
        client.DefaultRequestHeaders.Authorization = await ScopedAsync(client, global, slug); // no plan at all

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/tenant/roles")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/tenant/members")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/identity/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/onboarding/segments")).StatusCode);
    }

    [Fact]
    public async Task IdentityMeSurfacesTheEffectiveModulesInsideATenantSession()
    {
        await using var factory = ApiFactory.WithPersistentPlanProvider();
        using var client = factory.CreateClient();
        var (global, slug, tenantId) = await SetUpTenantAsync(client, "fe-me@nexora.test", "fe-me");
        await ConfigurePlanAsync(factory, tenantId, EveryModuleButReports, new Dictionary<string, int?> { ["PROFESSIONALS"] = 3 });
        client.DefaultRequestHeaders.Authorization = await ScopedAsync(client, global, slug);

        var me = await client.GetFromJsonAsync<MeResponse>("/api/v1/identity/me");
        Assert.NotNull(me!.Features);
        Assert.True(me.Features!["CUSTOMERS"].Enabled);
        Assert.False(me.Features["REPORTS"].Enabled);
        Assert.Equal(3, me.Features["PROFESSIONALS"].Limit);
    }

    [Fact]
    public async Task PermissionIsStillCheckedWhenThePlanIncludesTheModule()
    {
        await using var factory = ApiFactory.WithPersistentPlanProvider();
        using var client = factory.CreateClient();
        var (global, slug, tenantId) = await SetUpTenantAsync(client, "fe-rbac@nexora.test", "fe-rbac");
        await ConfigurePlanAsync(factory, tenantId, AllModules); // the plan grants REPORTS
        client.DefaultRequestHeaders.Authorization = await ScopedAsync(client, global, slug);

        // Reduce the owner to a role that can read customers but not reports.
        var role = await client.PostAsJsonAsync("/api/v1/tenant/roles",
            new { name = "Recepção", description = "x", permissions = CustomersReadOnly });
        role.EnsureSuccessStatusCode();
        var roleId = (await role.Content.ReadFromJsonAsync<IdRef>())!.Id;
        var members = await client.GetFromJsonAsync<IdRef[]>("/api/v1/tenant/members");
        (await client.PatchAsJsonAsync($"/api/v1/tenant/members/{members![0].Id}/role", new { roleId })).EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = await ScopedAsync(client, global, slug); // re-issue from the new role

        // REPORTS is in the plan, but the role lacks reports.read -> authorization rejects it,
        // and the rejection is not the feature gate's problem code.
        var blocked = await client.GetAsync($"/api/v1/reports/overview?from={From()}&to={To()}");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        // Authorization rejects it before the feature filter runs, so this is not a plan problem.
        Assert.DoesNotContain("feature_not_in_plan", await blocked.Content.ReadAsStringAsync());
        // The permission it still has, for a module the plan still includes, keeps working.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/customers")).StatusCode);
    }

    [Fact]
    public async Task ATenantOverrideNeverAffectsAnotherTenant()
    {
        await using var factory = ApiFactory.WithPersistentPlanProvider();
        using var clientA = factory.CreateClient();
        using var clientB = factory.CreateClient();
        var (globalA, slugA, tenantA) = await SetUpTenantAsync(clientA, "fe-iso-a@nexora.test", "fe-iso-a");
        await ConfigurePlanAsync(factory, tenantA, AllModules);
        var (globalB, slugB, tenantB) = await SetUpTenantAsync(clientB, "fe-iso-b@nexora.test", "fe-iso-b");
        await ConfigurePlanAsync(factory, tenantB, AllModules);
        await SetOverrideAsync(factory, tenantA, "REPORTS", enabled: false); // disable REPORTS for tenant A only

        clientA.DefaultRequestHeaders.Authorization = await ScopedAsync(clientA, globalA, slugA);
        clientB.DefaultRequestHeaders.Authorization = await ScopedAsync(clientB, globalB, slugB);

        Assert.Equal(HttpStatusCode.Forbidden, (await clientA.GetAsync($"/api/v1/reports/overview?from={From()}&to={To()}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await clientB.GetAsync($"/api/v1/reports/overview?from={From()}&to={To()}")).StatusCode);
    }

    [Fact]
    public async Task PastDueWithinGraceKeepsModulesButAnExpiredSubscriptionBlocksThemWhileCoreStays()
    {
        await using var factory = ApiFactory.WithPersistentPlanProvider();
        using var graceClient = factory.CreateClient();
        using var expiredClient = factory.CreateClient();
        var (graceGlobal, graceSlug, graceTenant) = await SetUpTenantAsync(graceClient, "fe-grace@nexora.test", "fe-grace");
        var (expGlobal, expSlug, expTenant) = await SetUpTenantAsync(expiredClient, "fe-expired@nexora.test", "fe-expired");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
            await TestFeatureCatalog.SeedFeaturesAsync(db);
            var features = await db.Features.Where(x => AllModules.Contains(x.Code)).ToListAsync();
            var now = DateTimeOffset.UtcNow;

            var grace = new Plan($"GRACE_{Guid.NewGuid():N}", "Grace", now);
            var expired = new Plan($"EXP_{Guid.NewGuid():N}", "Expired", now);
            foreach (var f in features)
            {
                grace.Features.Add(new PlanFeature(grace.Id, f.Id, true, null));
                expired.Features.Add(new PlanFeature(expired.Id, f.Id, true, null));
            }
            var graceSub = new Subscription(graceTenant, grace.Id, BillingInterval.Monthly, now.AddDays(-20), TimeSpan.FromDays(14));
            graceSub.Activate(now.AddDays(-5)); graceSub.MarkPastDue(now.AddDays(-3)); // 3 days into a 7-day grace
            var expiredSub = new Subscription(expTenant, expired.Id, BillingInterval.Monthly, now.AddDays(-40), TimeSpan.FromDays(14));
            expiredSub.Activate(now.AddDays(-20)); expiredSub.MarkPastDue(now.AddDays(-10)); // past the grace window
            db.Plans.AddRange(grace, expired);
            db.Subscriptions.AddRange(graceSub, expiredSub);
            await db.SaveChangesAsync();
        }

        graceClient.DefaultRequestHeaders.Authorization = await ScopedAsync(graceClient, graceGlobal, graceSlug);
        expiredClient.DefaultRequestHeaders.Authorization = await ScopedAsync(expiredClient, expGlobal, expSlug);

        Assert.Equal(HttpStatusCode.OK, (await graceClient.GetAsync("/api/v1/customers")).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await expiredClient.GetAsync("/api/v1/customers")).StatusCode);
        // Core stays reachable so an expired tenant can still see and settle its subscription.
        Assert.Equal(HttpStatusCode.OK, (await expiredClient.GetAsync("/api/v1/identity/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await expiredClient.GetAsync("/api/v1/subscription")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await expiredClient.GetAsync("/api/v1/tenant/members")).StatusCode);
    }

    [Fact]
    public async Task CustomerCreationIsRejectedOnceThePlanLimitIsReached()
    {
        await using var factory = ApiFactory.WithPersistentPlanProvider();
        using var client = factory.CreateClient();
        var (global, slug, tenantId) = await SetUpTenantAsync(client, "fe-cust-limit@nexora.test", "fe-cust-limit");
        await ConfigurePlanAsync(factory, tenantId, AllModules, new Dictionary<string, int?> { ["CUSTOMERS"] = 1 });
        client.DefaultRequestHeaders.Authorization = await ScopedAsync(client, global, slug);

        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/customers", new { name = "Cliente Um" })).StatusCode);
        var overLimit = await client.PostAsJsonAsync("/api/v1/customers", new { name = "Cliente Dois" });
        Assert.Equal(HttpStatusCode.Conflict, overLimit.StatusCode);
        var problem = await overLimit.Content.ReadFromJsonAsync<Problem>();
        Assert.Equal("plan_limit_reached", problem!.Code);
        Assert.Equal("CUSTOMERS", problem.Feature);
        Assert.Equal(1, problem.Limit);
    }

    // ---- helpers -----------------------------------------------------------------------------

    private static string From() => Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"));
    private static string To() => Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"));

    private static async Task ConfigurePlanAsync(
        ApiFactory factory, Guid tenantId, IReadOnlyCollection<string> enabled, Dictionary<string, int?>? limits = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        await TestFeatureCatalog.SeedFeaturesAsync(db);
        var features = await db.Features.Where(x => AllModules.Contains(x.Code)).ToListAsync();
        var plan = new Plan($"FE_{Guid.NewGuid():N}", "Feature enforcement plan", DateTimeOffset.UtcNow);
        foreach (var feature in features)
            plan.Features.Add(new PlanFeature(plan.Id, feature.Id, enabled.Contains(feature.Code),
                limits is not null && limits.TryGetValue(feature.Code, out var limit) ? limit : null));
        db.Plans.Add(plan);
        db.Subscriptions.Add(new Subscription(tenantId, plan.Id, BillingInterval.Monthly, DateTimeOffset.UtcNow, TimeSpan.FromDays(14)));
        await db.SaveChangesAsync();
    }

    private static async Task SetOverrideAsync(ApiFactory factory, Guid tenantId, string code, bool enabled)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        await TestFeatureCatalog.SeedFeaturesAsync(db);
        var featureId = await db.Features.Where(x => x.Code == code).Select(x => x.Id).SingleAsync();
        db.TenantFeatureOverrides.Add(new TenantFeatureOverride(tenantId, featureId, enabled, null));
        await db.SaveChangesAsync();
    }

    private static async Task<(string Global, string Slug, Guid TenantId)> SetUpTenantAsync(HttpClient client, string email, string slug)
    {
        var global = await RegisterAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", global);
        var response = await client.PostAsJsonAsync("/api/v1/tenants", new { name = slug, slug, timeZoneId = "UTC" });
        response.EnsureSuccessStatusCode();
        var id = (await response.Content.ReadFromJsonAsync<TenantDto>())!.Id;
        return (global, slug, id);
    }

    private static async Task<string> RegisterAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/identity/register", new { email, password = "Correct-Horse-42" });
        response.EnsureSuccessStatusCode(); SetCookie(client, response);
        return (await response.Content.ReadFromJsonAsync<Token>())!.AccessToken;
    }

    private static async Task<AuthenticationHeaderValue> ScopedAsync(HttpClient client, string token, string slug)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsJsonAsync($"/api/v1/t/{slug}/session", new { }); response.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Remove("Cookie"); SetCookie(client, response);
        return new AuthenticationHeaderValue("Bearer", (await response.Content.ReadFromJsonAsync<Token>())!.AccessToken);
    }

    private static void SetCookie(HttpClient client, HttpResponseMessage response) => client.DefaultRequestHeaders.Add("Cookie",
        response.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("nexora_refresh=", StringComparison.Ordinal)).Split(';', 2)[0]);

    private sealed record Token(string AccessToken, DateTimeOffset AccessTokenExpiresAt);
    private sealed record TenantDto(Guid Id);
    private sealed record IdRef(Guid Id);
    private sealed record Problem(string? Code, string? Feature, int? Limit, int? Current);
    private sealed record MeResponse(Guid Id, string Email, string[] Permissions, IReadOnlyDictionary<string, FeatureFlag>? Features);
    private sealed record FeatureFlag(bool Enabled, int? Limit);
}
