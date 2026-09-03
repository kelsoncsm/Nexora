using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Domain.Tenancy;
using Nexora.Infrastructure.Persistence;

namespace Nexora.IntegrationTests;

/// <summary>
/// No-privilege-escalation on <c>PUT /tenant/roles/{id}/permissions</c>: an actor may only set a
/// custom role's permissions to a subset of their own effective permissions — the same
/// <c>RoleGrant</c> invariant used on role assignment and invitations, with no <c>tenant.manage</c>
/// or system-role exception. Closes the "edit an existing role to grant a permission you lack" path.
/// </summary>
public sealed class RolePermissionEscalationTests
{
    private const string Password = "Correct-Horse-42";
    private static readonly string[] ManagePlusCustomers = ["tenant.manage", "customers.read"];
    private static readonly string[] ManageOnly = ["tenant.manage"];
    private static readonly string[] CustomersReadOnly = ["customers.read"];
    private static readonly string[] CustomersAndReports = ["customers.read", "reports.read"];
    private static readonly string[] CustomersReportsAppointments = ["customers.read", "reports.read", "appointments.read"];
    private static readonly string[] ManageAndCustomersRole = ["tenant.manage", "customers.read"];

    [Fact]
    public async Task ActorCannotAddToARoleAPermissionTheyDoNotHold()
    {
        await using var factory = new ApiFactory();
        using var owner = factory.CreateClient();
        var (_, slug, tenantId) = await OnboardAsync(owner, "esc-set-add@nexora.test", "esc-set-add");
        var target = await CreateRoleAsync(owner, "Atendimento", CustomersReadOnly);
        var actor = await SeedMemberAsync(factory, tenantId, "esc-set-add-a@nexora.test", "Gestor", ManagePlusCustomers);

        using var client = factory.CreateClient();
        await AuthenticateAsync(client, actor, slug);
        var response = await client.PutAsJsonAsync($"/api/v1/tenant/roles/{target}/permissions",
            new { permissions = CustomersAndReports }); // reports.read is above the actor

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ActorCanSetPermissionsEqualToTheirOwn()
    {
        await using var factory = new ApiFactory();
        using var owner = factory.CreateClient();
        var (_, slug, tenantId) = await OnboardAsync(owner, "esc-set-eq@nexora.test", "esc-set-eq");
        var target = await CreateRoleAsync(owner, "Atendimento", CustomersReadOnly);
        var actor = await SeedMemberAsync(factory, tenantId, "esc-set-eq-a@nexora.test", "Gestor", ManagePlusCustomers);

        using var client = factory.CreateClient();
        await AuthenticateAsync(client, actor, slug);
        var response = await client.PutAsJsonAsync($"/api/v1/tenant/roles/{target}/permissions",
            new { permissions = ManagePlusCustomers });

        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task ActorCanSetASubsetOfTheirOwnPermissions()
    {
        await using var factory = new ApiFactory();
        using var owner = factory.CreateClient();
        var (_, slug, tenantId) = await OnboardAsync(owner, "esc-set-sub@nexora.test", "esc-set-sub");
        var target = await CreateRoleAsync(owner, "Atendimento", ManageAndCustomersRole);
        var actor = await SeedMemberAsync(factory, tenantId, "esc-set-sub-a@nexora.test", "Gestor", ManagePlusCustomers);

        using var client = factory.CreateClient();
        await AuthenticateAsync(client, actor, slug);
        var response = await client.PutAsJsonAsync($"/api/v1/tenant/roles/{target}/permissions",
            new { permissions = CustomersReadOnly });

        response.EnsureSuccessStatusCode();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        var keys = await db.TenantRolePermissions.Where(x => x.TenantRoleId == target).Select(x => x.PermissionKey).ToListAsync();
        Assert.Equal(CustomersReadOnly, keys);
    }

    [Fact]
    public async Task TenantManageAloneDoesNotAllowGrantingArbitraryPermissions()
    {
        await using var factory = new ApiFactory();
        using var owner = factory.CreateClient();
        var (_, slug, tenantId) = await OnboardAsync(owner, "esc-set-manage@nexora.test", "esc-set-manage");
        var target = await CreateRoleAsync(owner, "Atendimento", CustomersReadOnly);
        // Actor holds ONLY tenant.manage — enough to reach the endpoint, nothing else.
        var actor = await SeedMemberAsync(factory, tenantId, "esc-set-manage-a@nexora.test", "SoGestao", ManageOnly);

        using var client = factory.CreateClient();
        await AuthenticateAsync(client, actor, slug);
        var response = await client.PutAsJsonAsync($"/api/v1/tenant/roles/{target}/permissions",
            new { permissions = CustomersReadOnly }); // customers.read is not held by the actor

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ARejectedAttemptLeavesTheRolePermissionsUnchanged()
    {
        await using var factory = new ApiFactory();
        using var owner = factory.CreateClient();
        var (_, slug, tenantId) = await OnboardAsync(owner, "esc-set-nochange@nexora.test", "esc-set-nochange");
        var target = await CreateRoleAsync(owner, "Atendimento", CustomersReadOnly);
        var actor = await SeedMemberAsync(factory, tenantId, "esc-set-nochange-a@nexora.test", "Gestor", ManagePlusCustomers);

        using var client = factory.CreateClient();
        await AuthenticateAsync(client, actor, slug);
        var rejected = await client.PutAsJsonAsync($"/api/v1/tenant/roles/{target}/permissions",
            new { permissions = CustomersReportsAppointments });
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        var keys = await db.TenantRolePermissions.Where(x => x.TenantRoleId == target).Select(x => x.PermissionKey).ToListAsync();
        Assert.Equal(CustomersReadOnly, keys); // untouched
    }

    // ---- helpers -------------------------------------------------------------------------------

    private static async Task<(string Global, string Slug, Guid TenantId)> OnboardAsync(HttpClient client, string email, string slug)
    {
        var global = await RegisterAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", global);
        var tenant = await client.PostAsJsonAsync("/api/v1/tenants", new { name = slug, slug, timeZoneId = "UTC" });
        tenant.EnsureSuccessStatusCode();
        var tenantId = (await tenant.Content.ReadFromJsonAsync<TenantDto>())!.Id;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await ScopedAsync(client, global, slug));
        return (global, slug, tenantId);
    }

    private static async Task<string> RegisterAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/identity/register", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        SetCookie(client, response);
        return (await response.Content.ReadFromJsonAsync<Token>())!.AccessToken;
    }

    private static async Task AuthenticateAsync(HttpClient client, string email, string slug)
    {
        var login = await client.PostAsJsonAsync("/api/v1/identity/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();
        SetCookie(client, login);
        var global = (await login.Content.ReadFromJsonAsync<Token>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await ScopedAsync(client, global, slug));
    }

    private static async Task<string> ScopedAsync(HttpClient client, string token, string slug)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsJsonAsync($"/api/v1/t/{slug}/session", new { });
        response.EnsureSuccessStatusCode();
        SetCookie(client, response);
        return (await response.Content.ReadFromJsonAsync<Token>())!.AccessToken;
    }

    private static void SetCookie(HttpClient client, HttpResponseMessage response)
    {
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", response.Headers.GetValues("Set-Cookie")
            .Single(x => x.StartsWith("nexora_refresh=", StringComparison.Ordinal)).Split(';', 2)[0]);
    }

    private static async Task<Guid> CreateRoleAsync(HttpClient client, string name, string[] permissions)
    {
        var response = await client.PostAsJsonAsync("/api/v1/tenant/roles", new { name, description = "seeded", permissions });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RoleRef>())!.Id;
    }

    private static async Task<string> SeedMemberAsync(ApiFactory factory, Guid tenantId, string email, string roleName, string[] permissions)
    {
        using var register = factory.CreateClient();
        (await register.PostAsJsonAsync("/api/v1/identity/register", new { email, password = Password })).EnsureSuccessStatusCode();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        var userId = await db.Users.Where(x => x.Email == email).Select(x => x.Id).SingleAsync();
        var role = new TenantRole(tenantId, roleName, "seeded", false);
        foreach (var key in permissions) role.Permissions.Add(new TenantRolePermission(role.Id, key));
        db.TenantRoles.Add(role);
        db.TenantUsers.Add(new TenantUser(tenantId, userId, role.Id, DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        return email;
    }

    private sealed record Token(string AccessToken, DateTimeOffset AccessTokenExpiresAt);
    private sealed record TenantDto(Guid Id);
    private sealed record RoleRef(Guid Id, bool IsSystem);
}
