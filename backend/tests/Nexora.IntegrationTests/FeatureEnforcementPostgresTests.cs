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
/// P1.1 / ADR-0019 on a real PostgreSQL database: the seed migration creates the module feature
/// catalog, and the endpoint filter + <c>PersistentTenantPlanProvider</c> + Subscription path
/// enforces it end to end. Gated by NEXORA_HARDENING_POSTGRES, like the other PG suites.
/// </summary>
[Collection("Postgres")]
public sealed class FeatureEnforcementPostgresTests
{
    [Fact]
    public async Task SeedMigrationCreatesTheFiveModuleFeatures()
    {
        if (Environment.GetEnvironmentVariable("NEXORA_HARDENING_POSTGRES") is null) return;
        await using var factory = new PostgresApiFactory();
        await factory.InitializeAsync();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();

        var codes = await db.Features.Where(x => x.IsActive).Select(x => x.Code).ToListAsync();
        foreach (var expected in new[] { "CUSTOMERS", "SCHEDULING", "PROFESSIONALS", "SERVICES", "REPORTS" })
            Assert.Contains(expected, codes);
    }

    [Fact]
    public async Task EnforcesPlanGrantsOverridesAndLimitsEndToEndOnPostgres()
    {
        if (Environment.GetEnvironmentVariable("NEXORA_HARDENING_POSTGRES") is null) return;
        var key = Guid.NewGuid().ToString("N");
        var slug = $"pg-feature-{key}";
        await using var factory = new PostgresApiFactory();
        await factory.InitializeAsync();
        using var client = factory.CreateClient();
        var token = await Register(client, $"{slug}@nexora.test");
        var tenantId = await CreateTenant(client, token, slug);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await Select(client, token, slug));

        // 1. No subscription -> every gated module answers 403 feature_not_in_plan.
        var noSub = await client.GetAsync("/api/v1/customers");
        Assert.Equal(HttpStatusCode.Forbidden, noSub.StatusCode);
        Assert.Equal("feature_not_in_plan", (await noSub.Content.ReadFromJsonAsync<Problem>())!.Code);

        // 2. A plan that grants CUSTOMERS + PROFESSIONALS (limit 1) but not REPORTS.
        Guid reportsFeatureId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
            var features = await db.Features.Where(x => new[] { "CUSTOMERS", "PROFESSIONALS", "REPORTS" }.Contains(x.Code)).ToListAsync();
            reportsFeatureId = features.Single(x => x.Code == "REPORTS").Id;
            var plan = new Plan($"PGF-{key}", "PG feature plan", DateTimeOffset.UtcNow);
            plan.Features.Add(new PlanFeature(plan.Id, features.Single(x => x.Code == "CUSTOMERS").Id, true, null));
            plan.Features.Add(new PlanFeature(plan.Id, features.Single(x => x.Code == "PROFESSIONALS").Id, true, 1));
            plan.Features.Add(new PlanFeature(plan.Id, reportsFeatureId, false, null));
            db.Plans.Add(plan);
            db.Subscriptions.Add(new Subscription(tenantId, plan.Id, BillingInterval.Monthly, DateTimeOffset.UtcNow, TimeSpan.FromDays(14)));
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/customers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(
            $"/api/v1/reports/overview?from={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"))}&to={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"))}")).StatusCode);

        // 3. Limit: first professional OK, second hits the plan limit with 409.
        Assert.Equal(HttpStatusCode.Created, (await client.PostAsJsonAsync("/api/v1/professionals", new { name = "Ana", isActive = true })).StatusCode);
        var overLimit = await client.PostAsJsonAsync("/api/v1/professionals", new { name = "Bruno", isActive = true });
        Assert.Equal(HttpStatusCode.Conflict, overLimit.StatusCode);
        Assert.Equal("plan_limit_reached", (await overLimit.Content.ReadFromJsonAsync<Problem>())!.Code);

        // 4. Override turns REPORTS on for this tenant only, effective on the next request.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
            db.TenantFeatureOverrides.Add(new TenantFeatureOverride(tenantId, reportsFeatureId, true, null));
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(
            $"/api/v1/reports/overview?from={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-1).ToString("O"))}&to={Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"))}")).StatusCode);
    }

    [Fact]
    public async Task ConcurrentCreatesCannotOvershootThePlanLimitOnPostgres()
    {
        if (Environment.GetEnvironmentVariable("NEXORA_HARDENING_POSTGRES") is null) return;
        var key = Guid.NewGuid().ToString("N");
        var slug = $"pg-limit-race-{key}";
        const int limit = 3;
        const int attempts = 10;
        await using var factory = new PostgresApiFactory();
        await factory.InitializeAsync();
        using var client = factory.CreateClient();
        var token = await Register(client, $"{slug}@nexora.test");
        var tenantId = await CreateTenant(client, token, slug);
        var scoped = await Select(client, token, slug);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
            var professionals = await db.Features.SingleAsync(x => x.Code == "PROFESSIONALS");
            var customers = await db.Features.SingleAsync(x => x.Code == "CUSTOMERS");
            var plan = new Plan($"PGL-{key}", "PG limit plan", DateTimeOffset.UtcNow);
            plan.Features.Add(new PlanFeature(plan.Id, professionals.Id, true, limit));
            plan.Features.Add(new PlanFeature(plan.Id, customers.Id, true, null));
            db.Plans.Add(plan);
            db.Subscriptions.Add(new Subscription(tenantId, plan.Id, BillingInterval.Monthly, DateTimeOffset.UtcNow, TimeSpan.FromDays(14)));
            await db.SaveChangesAsync();
        }

        // Fire more creates at once than the limit allows; each on its own client so they hit
        // the server on independent connections / request scopes.
        var results = await Task.WhenAll(Enumerable.Range(0, attempts).Select(async n =>
        {
            using var racer = factory.CreateClient();
            racer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", scoped);
            return (await racer.PostAsJsonAsync("/api/v1/professionals", new { name = $"P{n}", isActive = true })).StatusCode;
        }));

        Assert.Equal(limit, results.Count(x => x == HttpStatusCode.Created));
        Assert.Equal(attempts - limit, results.Count(x => x == HttpStatusCode.Conflict));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
            Assert.Equal(limit, await db.Professionals.CountAsync(x => x.TenantId == tenantId));
        }
    }

    private static async Task<string> Register(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/identity/register", new { email, password = "Correct-Horse-42" });
        response.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Add("Cookie", RefreshCookie(response));
        return (await response.Content.ReadFromJsonAsync<Token>())!.AccessToken;
    }

    private static async Task<Guid> CreateTenant(HttpClient client, string token, string slug)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsJsonAsync("/api/v1/tenants", new { name = slug, slug, timeZoneId = "UTC" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TenantDto>())!.Id;
    }

    private static async Task<string> Select(HttpClient client, string token, string slug)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsJsonAsync($"/api/v1/t/{slug}/session", new { });
        response.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", RefreshCookie(response));
        return (await response.Content.ReadFromJsonAsync<Token>())!.AccessToken;
    }

    private static string RefreshCookie(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("nexora_refresh=", StringComparison.Ordinal)).Split(';', 2)[0];

    private sealed record Token(string AccessToken);
    private sealed record TenantDto(Guid Id);
    private sealed record Problem(string? Code);
}
