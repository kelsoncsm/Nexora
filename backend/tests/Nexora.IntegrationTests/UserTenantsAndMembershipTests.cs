using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Nexora.IntegrationTests;

/// <summary>
/// P1.2 — <c>GET /api/v1/me/tenants</c> lets a returning user re-enter a company without typing a slug.
/// P1.4 — deactivating a team member is gated on <c>tenant.manage</c>, never <c>customers.delete</c>.
/// </summary>
public sealed class UserTenantsAndMembershipTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    // ---- P1.2 -----------------------------------------------------------------------------------

    [Fact]
    public async Task MeTenantsListsEveryActiveCompanyTheUserBelongsTo()
    {
        using var client = factory.CreateClient();
        var token = await RegisterAsync(client, "multi-tenant@nexora.test");
        client.DefaultRequestHeaders.Authorization = Bearer(token);
        await CreateTenantAsync(client, "Alpha Co", "alpha-co");
        await CreateTenantAsync(client, "Bravo Co", "bravo-co");

        var tenants = await client.GetFromJsonAsync<UserTenant[]>("/api/v1/me/tenants");

        Assert.Equal(2, tenants!.Length);
        Assert.Equal(["alpha-co", "bravo-co"], tenants.Select(x => x.Slug).OrderBy(x => x));
        Assert.All(tenants, x => Assert.Equal("ADMIN", x.RoleName));
    }

    [Fact]
    public async Task MeTenantsNeverLeaksAnotherUsersCompanies()
    {
        using var clientA = factory.CreateClient();
        using var clientB = factory.CreateClient();
        var tokenA = await RegisterAsync(clientA, "owner-a@nexora.test");
        var tokenB = await RegisterAsync(clientB, "owner-b@nexora.test");
        clientA.DefaultRequestHeaders.Authorization = Bearer(tokenA);
        clientB.DefaultRequestHeaders.Authorization = Bearer(tokenB);
        await CreateTenantAsync(clientA, "Only A", "only-a");
        await CreateTenantAsync(clientB, "Only B", "only-b");

        var tenantsA = await clientA.GetFromJsonAsync<UserTenant[]>("/api/v1/me/tenants");

        Assert.Equal("only-a", Assert.Single(tenantsA!).Slug);
    }

    [Fact]
    public async Task MeTenantsRequiresAuthentication()
    {
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me/tenants")).StatusCode);
    }

    [Fact]
    public async Task MeTenantsOmitsMembershipsThatAreNoLongerActive()
    {
        using var client = factory.CreateClient();
        var global = await RegisterAsync(client, "left-company@nexora.test");
        client.DefaultRequestHeaders.Authorization = Bearer(global);
        await CreateTenantAsync(client, "Leaving Co", "leaving-co");
        var scoped = await SelectTenantAsync(client, global, "leaving-co");

        client.DefaultRequestHeaders.Authorization = Bearer(scoped);
        var members = await client.GetFromJsonAsync<Member[]>("/api/v1/tenant/members");
        var self = Assert.Single(members!);
        (await client.PatchAsync($"/api/v1/tenant/members/{self.Id}/deactivate", null)).EnsureSuccessStatusCode();

        client.DefaultRequestHeaders.Authorization = Bearer(global);
        var tenants = await client.GetFromJsonAsync<UserTenant[]>("/api/v1/me/tenants");
        Assert.Empty(tenants!);
    }

    // ---- P1.4 -----------------------------------------------------------------------------------

    [Fact]
    public async Task MemberDeactivationSucceedsForARoleWithTenantManage()
    {
        using var client = factory.CreateClient();
        var global = await RegisterAsync(client, "manage-yes@nexora.test");
        client.DefaultRequestHeaders.Authorization = Bearer(global);
        await CreateTenantAsync(client, "Manage Co", "manage-co");
        var scoped = await SelectTenantAsync(client, global, "manage-co");
        client.DefaultRequestHeaders.Authorization = Bearer(scoped);

        var members = await client.GetFromJsonAsync<Member[]>("/api/v1/tenant/members");
        var owner = Assert.Single(members!); // ADMIN role carries tenant.manage

        var response = await client.PatchAsync($"/api/v1/tenant/members/{owner.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task CustomersDeleteAloneDoesNotAllowDeactivatingMembers()
    {
        using var client = factory.CreateClient();
        var global = await RegisterAsync(client, "manage-no@nexora.test");
        client.DefaultRequestHeaders.Authorization = Bearer(global);
        await CreateTenantAsync(client, "Reception Co", "reception-co");
        var scoped = await SelectTenantAsync(client, global, "reception-co");
        client.DefaultRequestHeaders.Authorization = Bearer(scoped);

        // A "reception" role able to delete customers but not to administer the tenant.
        var created = await client.PostAsJsonAsync("/api/v1/tenant/roles",
            new { name = "Recepção", description = "", permissions = ReceptionPermissions });
        created.EnsureSuccessStatusCode();
        var role = await created.Content.ReadFromJsonAsync<RoleView>();
        var members = await client.GetFromJsonAsync<Member[]>("/api/v1/tenant/members");
        var owner = Assert.Single(members!);
        (await client.PatchAsJsonAsync($"/api/v1/tenant/members/{owner.Id}/role", new { roleId = role!.Id })).EnsureSuccessStatusCode();

        var downgraded = await SelectTenantAsync(client, global, "reception-co");
        client.DefaultRequestHeaders.Authorization = Bearer(downgraded);

        var response = await client.PatchAsync($"/api/v1/tenant/members/{owner.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MemberDeactivationCannotCrossTenants()
    {
        using var clientA = factory.CreateClient();
        using var clientB = factory.CreateClient();
        var globalA = await RegisterAsync(clientA, "cross-a@nexora.test");
        var globalB = await RegisterAsync(clientB, "cross-b@nexora.test");
        clientA.DefaultRequestHeaders.Authorization = Bearer(globalA);
        clientB.DefaultRequestHeaders.Authorization = Bearer(globalB);
        await CreateTenantAsync(clientA, "Cross A", "cross-a");
        await CreateTenantAsync(clientB, "Cross B", "cross-b");
        var scopedA = await SelectTenantAsync(clientA, globalA, "cross-a");
        var scopedB = await SelectTenantAsync(clientB, globalB, "cross-b");

        clientB.DefaultRequestHeaders.Authorization = Bearer(scopedB);
        var memberB = Assert.Single((await clientB.GetFromJsonAsync<Member[]>("/api/v1/tenant/members"))!);

        clientA.DefaultRequestHeaders.Authorization = Bearer(scopedA);
        var response = await clientA.PatchAsync($"/api/v1/tenant/members/{memberB.Id}/deactivate", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        clientB.DefaultRequestHeaders.Authorization = Bearer(scopedB);
        Assert.True((await clientB.GetFromJsonAsync<Member[]>("/api/v1/tenant/members"))!.Single().IsActive);
    }

    // ---- helpers ------------------------------------------------------------------------------

    private static readonly string[] ReceptionPermissions = ["customers.read", "customers.delete"];

    private static async Task<string> RegisterAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/identity/register", new { email, password = "Correct-Horse-42" });
        response.EnsureSuccessStatusCode(); SetCookie(client, response);
        return (await response.Content.ReadFromJsonAsync<Token>())!.AccessToken;
    }
    private static async Task CreateTenantAsync(HttpClient client, string name, string slug)
    {
        var response = await client.PostAsJsonAsync("/api/v1/tenants", new { name, slug, timeZoneId = "UTC" });
        response.EnsureSuccessStatusCode();
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
    private sealed record UserTenant(Guid Id, string Name, string Slug, string RoleName);
    private sealed record RoleView(Guid Id, string Name, string Description, bool IsSystem, int MemberCount, string[] Permissions);
    private sealed record Member(Guid Id, Guid UserId, string Email, string Role, Guid RoleId, bool IsActive);
}
