using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Nexora.IntegrationTests;

/// <summary>
/// Company profile, roles and permissions endpoints: happy paths, the <c>tenant.manage</c> gate and
/// cross-tenant isolation (a tenant may never read or mutate another tenant's roles/permissions).
/// </summary>
public sealed class TenantAdministrationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task OwnerCanReadAndUpdateTheCompanyProfile()
    {
        using var client = factory.CreateClient();
        await OnboardTenantAsync(client, "admin-profile@nexora.test", "profile-co");

        var profile = await client.GetFromJsonAsync<Profile>("/api/v1/tenant");
        Assert.Equal("profile-co", profile!.Slug);

        var update = await client.PutAsJsonAsync("/api/v1/tenant", new { name = "Renamed Co", timeZoneId = "America/Sao_Paulo" });
        update.EnsureSuccessStatusCode();
        var updated = await update.Content.ReadFromJsonAsync<Profile>();
        Assert.Equal("Renamed Co", updated!.Name);
        Assert.Equal("America/Sao_Paulo", updated.TimeZoneId);
        Assert.Equal("profile-co", updated.Slug); // slug stays immutable
    }

    [Fact]
    public async Task ProfileUpdateRejectsInvalidTimeZone()
    {
        using var client = factory.CreateClient();
        await OnboardTenantAsync(client, "admin-tz@nexora.test", "tz-co");

        var bad = await client.PutAsJsonAsync("/api/v1/tenant", new { name = "TZ Co", timeZoneId = "Not/AZone" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task RolesCanBeCreatedUpdatedAndHaveTheirPermissionsReplaced()
    {
        using var client = factory.CreateClient();
        await OnboardTenantAsync(client, "admin-roles@nexora.test", "roles-co");

        var systemRoles = await client.GetFromJsonAsync<RoleView[]>("/api/v1/tenant/roles");
        Assert.Contains(systemRoles!, r => r.IsSystem && r.Name == "ADMIN");

        var createBody = new { name = "Gerente", description = "Gestão operacional", permissions = TwoReadPerms };
        var created = await client.PostAsJsonAsync("/api/v1/tenant/roles", createBody);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var role = await created.Content.ReadFromJsonAsync<RoleView>();
        Assert.Equal(2, role!.Permissions.Length);

        var renamed = await client.PutAsJsonAsync($"/api/v1/tenant/roles/{role.Id}", new { name = "Gerente Geral", description = "Gestão" });
        renamed.EnsureSuccessStatusCode();

        var setBody = new { permissions = ThreeCustomerPerms };
        var setPerms = await client.PutAsJsonAsync($"/api/v1/tenant/roles/{role.Id}/permissions", setBody);
        setPerms.EnsureSuccessStatusCode();
        var afterPerms = await setPerms.Content.ReadFromJsonAsync<RoleView>();
        Assert.Equal(3, afterPerms!.Permissions.Length);
        Assert.Contains("customers.create", afterPerms.Permissions);
    }

    [Fact]
    public async Task UnknownPermissionKeyIsRejected()
    {
        using var client = factory.CreateClient();
        await OnboardTenantAsync(client, "admin-badperm@nexora.test", "badperm-co");

        var body = new { name = "Broken", description = "", permissions = UnknownPerms };
        var bad = await client.PostAsJsonAsync("/api/v1/tenant/roles", body);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task SystemRoleCannotBeModified()
    {
        using var client = factory.CreateClient();
        await OnboardTenantAsync(client, "admin-system@nexora.test", "system-co");
        var roles = await client.GetFromJsonAsync<RoleView[]>("/api/v1/tenant/roles");
        var admin = Assert.Single(roles!, r => r.IsSystem);

        var rename = await client.PutAsJsonAsync($"/api/v1/tenant/roles/{admin.Id}", new { name = "SUPER", description = "" });
        Assert.Equal(HttpStatusCode.BadRequest, rename.StatusCode);

        var perms = await client.PutAsJsonAsync($"/api/v1/tenant/roles/{admin.Id}/permissions", new { permissions = OneReadPerm });
        Assert.Equal(HttpStatusCode.BadRequest, perms.StatusCode);
    }

    [Fact]
    public async Task PermissionCatalogListsEveryAssignableKey()
    {
        using var client = factory.CreateClient();
        await OnboardTenantAsync(client, "admin-catalog@nexora.test", "catalog-co");

        var catalog = await client.GetFromJsonAsync<PermissionDescriptor[]>("/api/v1/tenant/permissions");
        Assert.Contains(catalog!, p => p.Key == "customers.read" && p.Module == "customers" && p.Action == "read");
        Assert.Contains(catalog!, p => p.Key == "tenant.manage");
    }

    [Fact]
    public async Task WithoutTenantManageTheAdminEndpointsAreForbidden()
    {
        using var client = factory.CreateClient();
        var (global, slug) = await OnboardTenantAsync(client, "admin-downgrade@nexora.test", "downgrade-co");

        // Create a role without tenant.manage and move the only member onto it.
        var created = await client.PostAsJsonAsync("/api/v1/tenant/roles", new { name = "Operador", description = "", permissions = OneReadPerm });
        var role = await created.Content.ReadFromJsonAsync<RoleView>();
        var members = await client.GetFromJsonAsync<Member[]>("/api/v1/tenant/members");
        var owner = Assert.Single(members!);
        (await client.PatchAsJsonAsync($"/api/v1/tenant/members/{owner.Id}/role", new { roleId = role!.Id })).EnsureSuccessStatusCode();

        var scoped = await SelectTenantAsync(client, global, slug);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", scoped);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/v1/tenant/roles")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await client.PutAsJsonAsync("/api/v1/tenant", new { name = "X", timeZoneId = "UTC" })).StatusCode);
    }

    [Fact]
    public async Task TenantCannotReadOrMutateAnotherTenantsRole()
    {
        using var clientA = factory.CreateClient();
        using var clientB = factory.CreateClient();
        await OnboardTenantAsync(clientA, "iso-a@nexora.test", "iso-a");
        await OnboardTenantAsync(clientB, "iso-b@nexora.test", "iso-b");

        var createBody = new { name = "B-Only", description = "", permissions = OneReadPerm };
        var roleB = await (await clientB.PostAsJsonAsync("/api/v1/tenant/roles", createBody)).Content.ReadFromJsonAsync<RoleView>();

        // Tenant A tries to touch Tenant B's role id.
        Assert.Equal(HttpStatusCode.NotFound, (await clientA.GetAsync($"/api/v1/tenant/roles/{roleB!.Id}/permissions")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await clientA.PutAsJsonAsync($"/api/v1/tenant/roles/{roleB.Id}", new { name = "hijack", description = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await clientA.PutAsJsonAsync($"/api/v1/tenant/roles/{roleB.Id}/permissions", new { permissions = OneReportPerm })).StatusCode);

        var membersB = await clientB.GetFromJsonAsync<Member[]>("/api/v1/tenant/members");
        Assert.Equal(HttpStatusCode.NotFound,
            (await clientA.PatchAsJsonAsync($"/api/v1/tenant/members/{membersB!.Single().Id}/role", new { roleId = roleB.Id })).StatusCode);
    }

    private static readonly string[] OneReadPerm = ["customers.read"];
    private static readonly string[] OneReportPerm = ["reports.read"];
    private static readonly string[] TwoReadPerms = ["customers.read", "reports.read"];
    private static readonly string[] ThreeCustomerPerms = ["customers.read", "customers.create", "professionals.read"];
    private static readonly string[] UnknownPerms = ["customers.read", "made.up"];

    private static async Task<(string Global, string Slug)> OnboardTenantAsync(HttpClient client, string email, string slug)
    {
        var global = await RegisterAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", global);
        var tenant = await client.PostAsJsonAsync("/api/v1/tenants", new { name = slug, slug, timeZoneId = "UTC" });
        tenant.EnsureSuccessStatusCode();
        var scoped = await SelectTenantAsync(client, global, slug);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", scoped);
        return (global, slug);
    }

    private static async Task<string> RegisterAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/identity/register", new { email, password = "Correct-Horse-42" });
        response.EnsureSuccessStatusCode(); SetCookie(client, response);
        return (await response.Content.ReadFromJsonAsync<Token>())!.AccessToken;
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
    private sealed record Profile(Guid Id, string Name, string Slug, string TimeZoneId, bool IsActive, DateTimeOffset CreatedAt);
    private sealed record RoleView(Guid Id, string Name, string Description, bool IsSystem, int MemberCount, string[] Permissions);
    private sealed record PermissionDescriptor(string Key, string Module, string Action);
    private sealed record Member(Guid Id, Guid UserId, string Email, string Role, Guid RoleId, bool IsActive);
}
