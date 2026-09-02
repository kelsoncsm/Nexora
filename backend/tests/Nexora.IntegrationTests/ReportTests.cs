using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Application.Administration;
using Nexora.Application.Identity;
using Microsoft.EntityFrameworkCore;
using Nexora.Domain.Billing;
using Nexora.Domain.Plans;
using Nexora.Infrastructure.Persistence;

namespace Nexora.IntegrationTests;

public sealed class ReportTests
{
    [Fact]
    public async Task TenantReportIsAuthorizedFilteredAndIsolated()
    {
        await using var factory = new ApiFactory();
        await TestFeatureCatalog.GrantAllModulesAsync(factory);
        var tenantA = factory.CreateClient(); var tenantB = factory.CreateClient();
        var tokenA = await RegisterAsync(tenantA, "reports-a@nexora.test");
        var tokenB = await RegisterAsync(tenantB, "reports-b@nexora.test");
        await CreateTenantAsync(tenantA, tokenA, "reports-a"); await CreateTenantAsync(tenantB, tokenB, "reports-b");
        tenantA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await SelectTenantAsync(tenantA, tokenA, "reports-a"));
        tenantB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await SelectTenantAsync(tenantB, tokenB, "reports-b"));
        (await tenantA.PostAsJsonAsync("/api/v1/customers", new { name = "A", email = "a@test.local", phone = "1", notes = "" })).EnsureSuccessStatusCode();
        (await tenantB.PostAsJsonAsync("/api/v1/customers", new { name = "B", email = "b@test.local", phone = "2", notes = "" })).EnsureSuccessStatusCode();
        (await tenantB.PostAsJsonAsync("/api/v1/customers", new { name = "B2", email = "b2@test.local", phone = "3", notes = "" })).EnsureSuccessStatusCode();
        await using(var scope=factory.Services.CreateAsyncScope()){var db=scope.ServiceProvider.GetRequiredService<NexoraDbContext>();var tenantId=await db.Tenants.Where(x=>x.Slug=="reports-a").Select(x=>x.Id).SingleAsync();var now=DateTimeOffset.UtcNow;var plan=new Plan("REPORTS","Reports",now);var subscription=new Subscription(tenantId,plan.Id,BillingInterval.Monthly,now,TimeSpan.FromDays(14));var invoice=new BillingInvoice(tenantId,subscription.Id,plan.Id,BillingInterval.Monthly,123.45m,"BRL",subscription.TrialEndAt,subscription.TrialEndAt.AddMonths(1),now.AddDays(1),now);invoice.Apply(BillingPaymentStatus.Approved,now);db.AddRange(plan,subscription,invoice);await db.SaveChangesAsync();}

        var from = DateTimeOffset.UtcNow.AddDays(-1).ToString("O"); var to = DateTimeOffset.UtcNow.AddDays(1).ToString("O");
        var observable=await tenantA.GetFromJsonAsync<System.Text.Json.JsonElement>($"/api/v1/reports/overview?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}");Assert.False(observable.TryGetProperty("paidRevenue",out _));Assert.False(observable.TryGetProperty("revenue",out _));
        var reportA = await tenantA.GetFromJsonAsync<TenantReportResponse>($"/api/v1/reports/overview?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}");
        var reportB = await tenantB.GetFromJsonAsync<TenantReportResponse>($"/api/v1/reports/overview?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}");
        Assert.Equal(1, reportA!.Customers); Assert.Equal(2, reportB!.Customers);
        Assert.Equal(HttpStatusCode.BadRequest, (await tenantA.GetAsync($"/api/v1/reports/overview?from={Uri.EscapeDataString(to)}&to={Uri.EscapeDataString(from)}")).StatusCode);
    }

    [Fact]
    public async Task PlatformReportRequiresPlatformPermission()
    {
        await using var factory = new ApiFactory(); var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope(); var generator = scope.ServiceProvider.GetRequiredService<IAccessTokenGenerator>();
        var platformToken = generator.Generate(Guid.NewGuid(), "platform-reports@nexora.test", ["PlatformAdmin"], [PlatformPermissions.Access]).Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", platformToken);
        var from = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-30).ToString("O")); var to = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(1).ToString("O"));
        var response = await client.GetAsync($"/api/v1/admin/reports/overview?from={from}&to={to}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body=await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();Assert.True(body.TryGetProperty("paidRevenue",out _));
    }

    private static async Task<string> RegisterAsync(HttpClient client, string email)
    { var response = await client.PostAsJsonAsync("/api/v1/identity/register", new { email, password = "Correct-Horse-42" }); response.EnsureSuccessStatusCode(); SetCookie(client, response); return (await response.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken; }
    private static async Task CreateTenantAsync(HttpClient client, string token, string slug)
    { client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token); (await client.PostAsJsonAsync("/api/v1/tenants", new { name = slug, slug, timeZoneId = "America/Sao_Paulo" })).EnsureSuccessStatusCode(); }
    private static async Task<string> SelectTenantAsync(HttpClient client, string token, string slug)
    { client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token); var response = await client.PostAsJsonAsync($"/api/v1/t/{slug}/session", new { }); response.EnsureSuccessStatusCode(); client.DefaultRequestHeaders.Remove("Cookie"); SetCookie(client, response); return (await response.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken; }
    private static void SetCookie(HttpClient client, HttpResponseMessage response) => client.DefaultRequestHeaders.Add("Cookie", response.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("nexora_refresh=", StringComparison.Ordinal)).Split(';', 2)[0]);
    private sealed record TokenResponse(string AccessToken);
    private sealed record TenantReportResponse(int Customers);
}
