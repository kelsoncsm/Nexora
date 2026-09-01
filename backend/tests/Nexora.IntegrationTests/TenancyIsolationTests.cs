using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Nexora.IntegrationTests;

public sealed class TenancyIsolationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task TenantClaimIsolatesReadsWritesAndMaliciousSlug()
    {
        using var clientA = factory.CreateClient(); using var clientB = factory.CreateClient();
        var globalA = await RegisterAsync(clientA, "tenant-a-owner@nexora.test");
        var tenantA = await CreateTenantAsync(clientA, globalA, "Tenant A", "tenant-a");
        var scopedA = await SelectTenantAsync(clientA, globalA, tenantA.Slug);

        var globalB = await RegisterAsync(clientB, "tenant-b-owner@nexora.test");
        var tenantB = await CreateTenantAsync(clientB, globalB, "Tenant B", "tenant-b");
        var scopedB = await SelectTenantAsync(clientB, globalB, tenantB.Slug);

        Assert.Equal(HttpStatusCode.OK, (await clientA.GetAsync($"/api/v1/t/{tenantA.Slug}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await clientA.GetAsync("/api/v1/t/tenant-missing")).StatusCode);

        clientA.DefaultRequestHeaders.Authorization = Bearer(scopedA);
        var membersA = await clientA.GetFromJsonAsync<Member[]>($"/api/v1/t/{tenantA.Slug}/members");
        Assert.Single(membersA!);
        Assert.Equal(HttpStatusCode.NotFound, (await clientA.GetAsync($"/api/v1/t/{tenantB.Slug}/members")).StatusCode);

        clientB.DefaultRequestHeaders.Authorization = Bearer(scopedB);
        var membersB = await clientB.GetFromJsonAsync<Member[]>("/api/v1/tenant/members");
        var memberB = Assert.Single(membersB!);

        clientA.DefaultRequestHeaders.Authorization = Bearer(scopedA);
        var crossTenantUpdate = await clientA.PatchAsync($"/api/v1/tenant/members/{memberB.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.NotFound, crossTenantUpdate.StatusCode);

        clientB.DefaultRequestHeaders.Authorization = Bearer(scopedB);
        Assert.True((await clientB.GetFromJsonAsync<Member[]>("/api/v1/tenant/members"))!.Single().IsActive);

        clientB.DefaultRequestHeaders.Authorization = Bearer(globalB);
        var noMembership = await clientB.PostAsJsonAsync($"/api/v1/t/{tenantA.Slug}/session", new { tenantId = tenantA.Id });
        Assert.Equal(HttpStatusCode.Unauthorized, noMembership.StatusCode);
    }

    private static async Task<string> RegisterAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/identity/register", new { email, password = "Correct-Horse-42" });
        response.EnsureSuccessStatusCode(); SetCookie(client, response);
        return (await response.Content.ReadFromJsonAsync<Token>())!.AccessToken;
    }
    private static async Task<TenantResponse> CreateTenantAsync(HttpClient client, string token, string name, string slug)
    {
        client.DefaultRequestHeaders.Authorization = Bearer(token);
        var response = await client.PostAsJsonAsync("/api/v1/tenants", new { name, slug, timeZoneId = "UTC" }); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TenantResponse>())!;
    }
    private static async Task<string> SelectTenantAsync(HttpClient client, string token, string slug)
    {
        client.DefaultRequestHeaders.Authorization = Bearer(token);
        var response = await client.PostAsJsonAsync($"/api/v1/t/{slug}/session", new { }); response.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Remove("Cookie"); SetCookie(client, response);
        return (await response.Content.ReadFromJsonAsync<Token>())!.AccessToken;
    }
    private static void SetCookie(HttpClient client, HttpResponseMessage response) => client.DefaultRequestHeaders.Add("Cookie",
        response.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("nexora_refresh=", StringComparison.Ordinal)).Split(';', 2)[0]);
    private static AuthenticationHeaderValue Bearer(string token) => new("Bearer", token);
    private sealed record Token(string AccessToken, DateTimeOffset AccessTokenExpiresAt);
    private sealed record TenantResponse(Guid Id, string Name, string Slug);
    private sealed record Member(Guid Id, Guid UserId, string Email, string Role, bool IsActive);
}
