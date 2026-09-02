using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Application.Billing;
using Nexora.Domain.Billing;
using Nexora.Domain.Plans;
using Nexora.Infrastructure.Persistence;

namespace Nexora.IntegrationTests;

/// <summary>
/// Audit P2.1 / P2.2 — the tenant-facing Billing/Subscription endpoints and GET /tenant/members
/// require <c>tenant.manage</c>, like the rest of the tenant admin surface. Feature and permission
/// stay independent; billing is never feature-gated, so an expired tenant with the permission can
/// still see and settle its subscription (F5 / ADR-0019). Cross-tenant reads stay isolated.
/// </summary>
public sealed class BillingAndMembershipAuthorizationTests
{
    private static readonly string[] CustomersReadOnly = ["customers.read"];

    [Fact]
    public async Task BillingAndSubscriptionEndpointsRequireTenantManage()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var (global, slug, _) = await SetUpTenantAsync(client, "authz-billing@nexora.test", "authz-billing");
        client.DefaultRequestHeaders.Authorization = await ScopedAsync(client, global, slug);

        // Owner (system ADMIN role) carries tenant.manage.
        Assert.NotEqual(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/subscription")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/billing/invoices")).StatusCode);

        await ReduceSelfToRoleAsync(client, global, slug, "Recepção", CustomersReadOnly);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/subscription")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/billing/invoices")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PostAsJsonAsync("/api/v1/billing/checkout", new { paymentMethod = "Pix", payerEmail = "x@nexora.test" })).StatusCode);
    }

    [Fact]
    public async Task APastDueTenantOwnerCanStillSeeAndSettleTheSubscription()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var (global, slug, tenantId) = await SetUpTenantAsync(client, "authz-pastdue@nexora.test", "authz-pastdue");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
            var now = DateTimeOffset.UtcNow;
            var plan = new Plan("PASTDUE", "PastDue", now);
            var sub = new Subscription(tenantId, plan.Id, BillingInterval.Monthly, now.AddDays(-20), TimeSpan.FromDays(14));
            sub.Activate(now.AddDays(-6));
            sub.MarkPastDue(now.AddDays(-2)); // behind on payment, still needs to regularise
            db.AddRange(plan, sub, new PlanPrice(plan.Id, BillingInterval.Monthly, "BRL", 50, now));
            await db.SaveChangesAsync();
        }
        factory.PaymentGateway.CheckoutResult = new("mp-pastdue", BillingPaymentStatus.Pending, "https://checkout.test/mp-pastdue", null, DateTimeOffset.UtcNow);
        client.DefaultRequestHeaders.Authorization = await ScopedAsync(client, global, slug);

        // Feature enforcement does not touch billing — a behind-on-payment owner keeps the regularization path.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/subscription")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/api/v1/billing/checkout", new { paymentMethod = "Pix", payerEmail = "authz-pastdue@nexora.test" })).StatusCode);
    }

    [Fact]
    public async Task TenantBillingDataIsIsolated()
    {
        await using var factory = new ApiFactory();
        using var a = factory.CreateClient();
        using var b = factory.CreateClient();
        var (globalA, slugA, tenantA) = await SetUpTenantAsync(a, "authz-iso-a@nexora.test", "authz-iso-a");
        var (globalB, slugB, _) = await SetUpTenantAsync(b, "authz-iso-b@nexora.test", "authz-iso-b");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
            var now = DateTimeOffset.UtcNow;
            var plan = new Plan("ISO", "Iso", now);
            var sub = new Subscription(tenantA, plan.Id, BillingInterval.Monthly, now, TimeSpan.FromDays(14));
            db.AddRange(plan, sub, new PlanPrice(plan.Id, BillingInterval.Monthly, "BRL", 30, now));
            await db.SaveChangesAsync();
        }
        factory.PaymentGateway.CheckoutResult = new("mp-iso", BillingPaymentStatus.Pending, null, null, DateTimeOffset.UtcNow);

        a.DefaultRequestHeaders.Authorization = await ScopedAsync(a, globalA, slugA);
        (await a.PostAsJsonAsync("/api/v1/billing/checkout", new { paymentMethod = "Pix", payerEmail = "authz-iso-a@nexora.test" })).EnsureSuccessStatusCode();
        Assert.Single((await a.GetFromJsonAsync<System.Text.Json.JsonElement[]>("/api/v1/billing/invoices"))!);

        b.DefaultRequestHeaders.Authorization = await ScopedAsync(b, globalB, slugB);
        Assert.Empty((await b.GetFromJsonAsync<System.Text.Json.JsonElement[]>("/api/v1/billing/invoices"))!);
    }

    [Fact]
    public async Task ListingTenantMembersRequiresTenantManage()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var (global, slug, _) = await SetUpTenantAsync(client, "authz-members@nexora.test", "authz-members");
        client.DefaultRequestHeaders.Authorization = await ScopedAsync(client, global, slug);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/tenant/members")).StatusCode);

        // A role with an unrelated permission is still not allowed to list members.
        await ReduceSelfToRoleAsync(client, global, slug, "Recepção", CustomersReadOnly);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/tenant/members")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/t/{slug}/members")).StatusCode);
    }

    [Fact]
    public async Task TenantMembersAreNotVisibleAcrossTenants()
    {
        await using var factory = new ApiFactory();
        using var a = factory.CreateClient();
        using var b = factory.CreateClient();
        var (globalA, slugA, _) = await SetUpTenantAsync(a, "authz-m-a@nexora.test", "authz-m-a");
        await SetUpTenantAsync(b, "authz-m-b@nexora.test", "authz-m-b");

        a.DefaultRequestHeaders.Authorization = await ScopedAsync(a, globalA, slugA);
        var members = await a.GetFromJsonAsync<Member[]>("/api/v1/tenant/members");
        var only = Assert.Single(members!);
        Assert.Equal("authz-m-a@nexora.test", only.Email);
    }

    // ---- helpers -----------------------------------------------------------------------------

    private static async Task<(string Global, string Slug, Guid TenantId)> SetUpTenantAsync(HttpClient client, string email, string slug)
    {
        var global = await RegisterAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", global);
        var response = await client.PostAsJsonAsync("/api/v1/tenants", new { name = slug, slug, timeZoneId = "UTC" });
        response.EnsureSuccessStatusCode();
        var id = (await response.Content.ReadFromJsonAsync<TenantDto>())!.Id;
        return (global, slug, id);
    }

    private static async Task ReduceSelfToRoleAsync(HttpClient client, string global, string slug, string name, string[] permissions)
    {
        var role = await client.PostAsJsonAsync("/api/v1/tenant/roles", new { name, description = "x", permissions });
        role.EnsureSuccessStatusCode();
        var roleId = (await role.Content.ReadFromJsonAsync<IdRef>())!.Id;
        var members = await client.GetFromJsonAsync<IdRef[]>("/api/v1/tenant/members");
        (await client.PatchAsJsonAsync($"/api/v1/tenant/members/{members![0].Id}/role", new { roleId })).EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = await ScopedAsync(client, global, slug);
    }

    private static async Task<string> RegisterAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/identity/register", new { email, password = "Correct-Horse-42" });
        response.EnsureSuccessStatusCode();
        SetCookie(client, response);
        return (await response.Content.ReadFromJsonAsync<Token>())!.AccessToken;
    }

    private static async Task<AuthenticationHeaderValue> ScopedAsync(HttpClient client, string token, string slug)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsJsonAsync($"/api/v1/t/{slug}/session", new { });
        response.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Remove("Cookie");
        SetCookie(client, response);
        return new AuthenticationHeaderValue("Bearer", (await response.Content.ReadFromJsonAsync<Token>())!.AccessToken);
    }

    private static void SetCookie(HttpClient client, HttpResponseMessage response) => client.DefaultRequestHeaders.Add("Cookie",
        response.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("nexora_refresh=", StringComparison.Ordinal)).Split(';', 2)[0]);

    private sealed record Token(string AccessToken, DateTimeOffset AccessTokenExpiresAt);
    private sealed record TenantDto(Guid Id);
    private sealed record IdRef(Guid Id);
    private sealed record Member(Guid Id, string Email);
}
