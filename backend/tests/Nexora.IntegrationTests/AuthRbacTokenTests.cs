using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Nexora.IntegrationTests;

/// <summary>
/// Locks in the AUTH/RBAC gate fix: tenant-scoped permissions must travel inside the JWT after a
/// tenant is selected (so the SPA sidebar sees them), while the global identity permission survives.
/// </summary>
public sealed class AuthRbacTokenTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly string[] ExpectedTenantPermissions =
        ["customers.read", "professionals.read", "services.read", "appointments.read", "reports.read", "tenant.manage"];
    private static readonly string[] CustomersReadOnly = ["customers.read"];

    [Fact]
    public async Task PlainLoginTokenCarriesOnlyGlobalIdentityPermissions()
    {
        using var client = factory.CreateClient();
        var token = await RegisterAsync(client, "rbac-plain@nexora.test");

        var permissions = PermissionsOf(token);

        Assert.Contains("identity.profile", permissions);
        Assert.DoesNotContain("customers.read", permissions);
        Assert.DoesNotContain("tenant.manage", permissions);
    }

    [Fact]
    public async Task TenantSessionTokenCarriesTenantPermissionsAndKeepsIdentityProfile()
    {
        using var client = factory.CreateClient();
        var global = await RegisterAsync(client, "rbac-admin@nexora.test");
        var tenant = await CreateTenantAsync(client, global, "RBAC Co", "rbac-co");
        var scoped = await SelectTenantAsync(client, global, tenant.Slug);

        var permissions = PermissionsOf(scoped);

        foreach (var expected in ExpectedTenantPermissions)
            Assert.Contains(expected, permissions);
        // identity.profile must survive the tenant permission swap so /identity/me stays reachable.
        Assert.Contains("identity.profile", permissions);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", scoped);
        var me = await client.GetAsync("/api/v1/identity/me");
        me.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ReducedRoleTokenOnlyCarriesTheGrantsItWasGiven()
    {
        using var client = factory.CreateClient();
        var global = await RegisterAsync(client, "rbac-reduced@nexora.test");
        var tenant = await CreateTenantAsync(client, global, "Reduced Co", "reduced-co");
        var scoped = await SelectTenantAsync(client, global, tenant.Slug);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", scoped);

        // A custom role limited to reading customers only.
        var create = await client.PostAsJsonAsync("/api/v1/tenant/roles",
            new { name = "Recepção", description = "Somente clientes", permissions = CustomersReadOnly });
        create.EnsureSuccessStatusCode();
        var role = await create.Content.ReadFromJsonAsync<RoleView>();

        var members = await client.GetFromJsonAsync<Member[]>("/api/v1/tenant/members");
        var owner = Assert.Single(members!);
        var assign = await client.PatchAsJsonAsync($"/api/v1/tenant/members/{owner.Id}/role", new { roleId = role!.Id });
        assign.EnsureSuccessStatusCode();

        // Re-selecting the tenant re-issues the token from the new role.
        var reScoped = await SelectTenantAsync(client, global, tenant.Slug);
        var permissions = PermissionsOf(reScoped);

        Assert.Contains("customers.read", permissions);
        Assert.Contains("identity.profile", permissions);
        Assert.DoesNotContain("tenant.manage", permissions);
        Assert.DoesNotContain("reports.read", permissions);
    }

    private static string[] PermissionsOf(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var json = Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/').PadRight(payload.Length + (4 - payload.Length % 4) % 4, '='));
        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(json));
        if (!doc.RootElement.TryGetProperty("permission", out var claim)) return [];
        return claim.ValueKind == JsonValueKind.Array
            ? claim.EnumerateArray().Select(x => x.GetString()!).ToArray()
            : [claim.GetString()!];
    }

    private static async Task<string> RegisterAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/identity/register", new { email, password = "Correct-Horse-42" });
        response.EnsureSuccessStatusCode(); SetCookie(client, response);
        return (await response.Content.ReadFromJsonAsync<Token>())!.AccessToken;
    }
    private static async Task<TenantResponse> CreateTenantAsync(HttpClient client, string token, string name, string slug)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsJsonAsync("/api/v1/tenants", new { name, slug, timeZoneId = "UTC" }); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TenantResponse>())!;
    }
    private static async Task<string> SelectTenantAsync(HttpClient client, string token, string slug)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsJsonAsync($"/api/v1/t/{slug}/session", new { }); response.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Remove("Cookie"); SetCookie(client, response);
        return (await response.Content.ReadFromJsonAsync<Token>())!.AccessToken;
    }
    private static void SetCookie(HttpClient client, HttpResponseMessage response) => client.DefaultRequestHeaders.Add("Cookie",
        response.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("nexora_refresh=", StringComparison.Ordinal)).Split(';', 2)[0]);

    private sealed record Token(string AccessToken, DateTimeOffset AccessTokenExpiresAt);
    private sealed record TenantResponse(Guid Id, string Name, string Slug);
    private sealed record RoleView(Guid Id, string Name, string Description, bool IsSystem, int MemberCount, string[] Permissions);
    private sealed record Member(Guid Id, Guid UserId, string Email, string Role, Guid RoleId, bool IsActive);
}
