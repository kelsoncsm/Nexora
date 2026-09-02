using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Nexora.IntegrationTests;

/// <summary>
/// Runs the tenant administration + AUTH/RBAC guarantees against real PostgreSQL, where the tenant
/// permission joins and the <c>tenant.manage</c> backfill migration actually execute.
/// </summary>
[Collection("Postgres")]
public sealed class TenantAdministrationPostgresTests
{
    private static readonly string[] CustomersReadOnly = ["customers.read"];
    private static readonly string[] ReportsReadOnly = ["reports.read"];

    [Fact]
    public async Task TenantSessionTokenAndRoleIsolationHoldOnPostgres()
    {
        if (Environment.GetEnvironmentVariable("NEXORA_HARDENING_POSTGRES") is null) return;

        await using var factory = new PostgresApiFactory();
        await factory.InitializeAsync();

        var key = Guid.NewGuid().ToString("N")[..8];
        using var clientA = factory.CreateClient();
        using var clientB = factory.CreateClient();
        var slugA = $"pg-adm-a-{key}";
        var slugB = $"pg-adm-b-{key}";
        var (globalA, _) = await Onboard(clientA, $"{slugA}@nexora.test", slugA);
        await Onboard(clientB, $"{slugB}@nexora.test", slugB);

        // Token carries tenant grants + identity.profile after selecting the tenant.
        var scopedA = await Select(clientA, globalA, slugA);
        var perms = PermissionsOf(scopedA);
        Assert.Contains("customers.read", perms);
        Assert.Contains("tenant.manage", perms);
        Assert.Contains("identity.profile", perms);

        // Cross-tenant role access is rejected.
        var roleBody = new { name = $"B-{key}", description = "", permissions = CustomersReadOnly };
        var roleB = await (await clientB.PostAsJsonAsync("/api/v1/tenant/roles", roleBody)).Content.ReadFromJsonAsync<RoleView>();
        clientA.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", scopedA);
        Assert.Equal(HttpStatusCode.NotFound, (await clientA.GetAsync($"/api/v1/tenant/roles/{roleB!.Id}/permissions")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await clientA.PutAsJsonAsync($"/api/v1/tenant/roles/{roleB.Id}/permissions", new { permissions = ReportsReadOnly })).StatusCode);

        // Replacing a role's permissions persists through the DB round-trip.
        var custom = await (await clientA.PostAsJsonAsync("/api/v1/tenant/roles",
            new { name = $"A-{key}", description = "", permissions = CustomersReadOnly })).Content.ReadFromJsonAsync<RoleView>();
        var updated = await (await clientA.PutAsJsonAsync($"/api/v1/tenant/roles/{custom!.Id}/permissions",
            new { permissions = ReportsReadOnly })).Content.ReadFromJsonAsync<RoleView>();
        Assert.Equal(ReportsReadOnly, updated!.Permissions);
    }

    private static async Task<(string Global, string Slug)> Onboard(HttpClient client, string email, string slug)
    {
        var global = await Register(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", global);
        (await client.PostAsJsonAsync("/api/v1/tenants", new { name = slug, slug, timeZoneId = "UTC" })).EnsureSuccessStatusCode();
        var scoped = await Select(client, global, slug);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", scoped);
        return (global, slug);
    }

    private static async Task<string> Register(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/identity/register", new { email, password = "Correct-Horse-42" });
        response.EnsureSuccessStatusCode(); SetCookie(client, response);
        return (await response.Content.ReadFromJsonAsync<Token>())!.AccessToken;
    }
    private static async Task<string> Select(HttpClient client, string token, string slug)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsJsonAsync($"/api/v1/t/{slug}/session", new { }); response.EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Remove("Cookie"); SetCookie(client, response);
        return (await response.Content.ReadFromJsonAsync<Token>())!.AccessToken;
    }
    private static void SetCookie(HttpClient client, HttpResponseMessage response) => client.DefaultRequestHeaders.Add("Cookie",
        response.Headers.GetValues("Set-Cookie").Single(x => x.StartsWith("nexora_refresh=", StringComparison.Ordinal)).Split(';', 2)[0]);

    private static string[] PermissionsOf(string jwt)
    {
        var payload = jwt.Split('.')[1];
        var bytes = Convert.FromBase64String(payload.Replace('-', '+').Replace('_', '/').PadRight(payload.Length + (4 - payload.Length % 4) % 4, '='));
        using var doc = System.Text.Json.JsonDocument.Parse(System.Text.Encoding.UTF8.GetString(bytes));
        if (!doc.RootElement.TryGetProperty("permission", out var claim)) return [];
        return claim.ValueKind == System.Text.Json.JsonValueKind.Array
            ? claim.EnumerateArray().Select(x => x.GetString()!).ToArray()
            : [claim.GetString()!];
    }

    private sealed record Token(string AccessToken, DateTimeOffset AccessTokenExpiresAt);
    private sealed record RoleView(Guid Id, string Name, string Description, bool IsSystem, int MemberCount, string[] Permissions);
}
