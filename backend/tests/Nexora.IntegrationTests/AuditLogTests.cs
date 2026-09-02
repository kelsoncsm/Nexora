using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Domain.Tenancy;
using Nexora.Infrastructure.Persistence;

namespace Nexora.IntegrationTests;

/// <summary>
/// P2.9 / ADR-0021 — sensitive tenant-administration actions are recorded in <c>audit_logs</c>
/// with the human actor, the tenant, and a structured Details diff. A rejected action leaves no
/// entry; a no-op change is not recorded; cross-tenant attempts neither mutate nor audit.
/// </summary>
public sealed class AuditLogTests
{
    private static readonly string[] CustomersReadOnly = ["customers.read"];
    private static readonly string[] CustomersReadWrite = ["customers.read", "customers.create"];
    private static readonly string[] CustomersReadWriteReversed = ["customers.create", "customers.read"];
    private static readonly string[] CreateAndAppointmentsRead = ["customers.create", "appointments.read"];

    [Fact]
    public async Task MemberRoleChangeIsAudited()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var (global, slug, tenantId) = await SetUpTenantAsync(client, "audit-role@nexora.test", "audit-role");
        client.DefaultRequestHeaders.Authorization = await ScopedAsync(client, global, slug);

        var target = await SeedMemberAsync(factory, tenantId, "audit-role-member@nexora.test", "Staff", CustomersReadOnly);
        var newRole = await CreateRoleAsync(client, "Gerente", CustomersReadWrite);
        var actorId = await UserIdAsync(factory, "AUDIT-ROLE@NEXORA.TEST");

        (await client.PatchAsJsonAsync($"/api/v1/tenant/members/{target.MembershipId}/role", new { roleId = newRole })).EnsureSuccessStatusCode();

        var entry = await SingleAuditAsync(factory, "member.role_changed");
        Assert.Equal(actorId, entry.ActorUserId);
        Assert.Equal(tenantId, entry.TenantId);
        Assert.Equal("TenantMembership", entry.TargetType);
        Assert.Equal(target.MembershipId.ToString(), entry.TargetId);
        var details = JsonSerializer.Deserialize<JsonElement>(entry.Details!);
        Assert.Equal(target.RoleId.ToString(), details.GetProperty("oldRoleId").GetString());
        Assert.Equal(newRole.ToString(), details.GetProperty("newRoleId").GetString());
    }

    [Fact]
    public async Task MemberRoleChangeWithoutTenantManageIsRejectedAndNotAudited()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var (global, slug, tenantId) = await SetUpTenantAsync(client, "audit-role-403@nexora.test", "audit-role-403");
        client.DefaultRequestHeaders.Authorization = await ScopedAsync(client, global, slug);
        var target = await SeedMemberAsync(factory, tenantId, "audit-role-403-m@nexora.test", "Staff", CustomersReadOnly);
        var otherRole = await CreateRoleAsync(client, "Outro", CustomersReadWrite);
        await ReduceSelfToRoleAsync(client, global, slug, CustomersReadOnly); // this self-assignment is itself audited
        var auditedBefore = await CountAuditAsync(factory, "member.role_changed");

        var response = await client.PatchAsJsonAsync($"/api/v1/tenant/members/{target.MembershipId}/role", new { roleId = otherRole });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(auditedBefore, await CountAuditAsync(factory, "member.role_changed"));
    }

    [Fact]
    public async Task MemberDeactivationIsAudited()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var (global, slug, tenantId) = await SetUpTenantAsync(client, "audit-deact@nexora.test", "audit-deact");
        client.DefaultRequestHeaders.Authorization = await ScopedAsync(client, global, slug);
        var target = await SeedMemberAsync(factory, tenantId, "audit-deact-m@nexora.test", "Staff", CustomersReadOnly);

        (await client.PatchAsync($"/api/v1/tenant/members/{target.MembershipId}/deactivate", null)).EnsureSuccessStatusCode();

        var entry = await SingleAuditAsync(factory, "member.deactivated");
        Assert.Equal(tenantId, entry.TenantId);
        Assert.Equal(target.MembershipId.ToString(), entry.TargetId);
        Assert.True(JsonSerializer.Deserialize<JsonElement>(entry.Details!).GetProperty("wasActive").GetBoolean());
    }

    [Fact]
    public async Task CrossTenantDeactivationNeitherMutatesNorAudits()
    {
        await using var factory = new ApiFactory();
        using var a = factory.CreateClient();
        using var b = factory.CreateClient();
        var (globalA, slugA, _) = await SetUpTenantAsync(a, "audit-x-a@nexora.test", "audit-x-a");
        var (globalB, slugB, tenantB) = await SetUpTenantAsync(b, "audit-x-b@nexora.test", "audit-x-b");
        var victim = await SeedMemberAsync(factory, tenantB, "audit-x-victim@nexora.test", "Staff", CustomersReadOnly);

        a.DefaultRequestHeaders.Authorization = await ScopedAsync(a, globalA, slugA);
        var response = await a.PatchAsync($"/api/v1/tenant/members/{victim.MembershipId}/deactivate", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(0, await CountAuditAsync(factory, "member.deactivated"));
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.True((await scope.ServiceProvider.GetRequiredService<NexoraDbContext>().TenantUsers.SingleAsync(x => x.Id == victim.MembershipId)).IsActive);
    }

    [Fact]
    public async Task RolePermissionChangeRecordsTheAddedAndRemovedKeys()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var (global, slug, tenantId) = await SetUpTenantAsync(client, "audit-perm@nexora.test", "audit-perm");
        client.DefaultRequestHeaders.Authorization = await ScopedAsync(client, global, slug);
        var roleId = await CreateRoleAsync(client, "Recepção", CustomersReadOnly);

        (await client.PutAsJsonAsync($"/api/v1/tenant/roles/{roleId}/permissions",
            new { permissions = CreateAndAppointmentsRead })).EnsureSuccessStatusCode();

        var entry = await SingleAuditAsync(factory, "role.permissions_changed");
        Assert.Equal(tenantId, entry.TenantId);
        var details = JsonSerializer.Deserialize<JsonElement>(entry.Details!);
        var added = details.GetProperty("added").EnumerateArray().Select(x => x.GetString()!).ToHashSet(StringComparer.Ordinal);
        var removed = details.GetProperty("removed").EnumerateArray().Select(x => x.GetString()!).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(CreateAndAppointmentsRead.ToHashSet(StringComparer.Ordinal), added);
        Assert.Equal(CustomersReadOnly.ToHashSet(StringComparer.Ordinal), removed);
    }

    [Fact]
    public async Task RolePermissionNoOpIsNotAudited()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var (global, slug, _) = await SetUpTenantAsync(client, "audit-perm-noop@nexora.test", "audit-perm-noop");
        client.DefaultRequestHeaders.Authorization = await ScopedAsync(client, global, slug);
        var roleId = await CreateRoleAsync(client, "Recepção", CustomersReadWrite);

        // Same set, different order — no diff, no event.
        (await client.PutAsJsonAsync($"/api/v1/tenant/roles/{roleId}/permissions",
            new { permissions = CustomersReadWriteReversed })).EnsureSuccessStatusCode();

        Assert.Equal(0, await CountAuditAsync(factory, "role.permissions_changed"));
    }

    [Fact]
    public async Task AFailedPermissionChangeLeavesNoAuditEntry()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var (global, slug, _) = await SetUpTenantAsync(client, "audit-perm-fail@nexora.test", "audit-perm-fail");
        client.DefaultRequestHeaders.Authorization = await ScopedAsync(client, global, slug);
        var systemRole = (await client.GetFromJsonAsync<RoleRef[]>("/api/v1/tenant/roles"))!.Single(x => x.IsSystem);

        var response = await client.PutAsJsonAsync($"/api/v1/tenant/roles/{systemRole.Id}/permissions",
            new { permissions = CustomersReadOnly });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, await CountAuditAsync(factory, "role.permissions_changed"));
    }

    // ---- helpers -----------------------------------------------------------------------------

    private sealed record SeededMember(Guid MembershipId, Guid RoleId, Guid UserId);

    private static async Task<SeededMember> SeedMemberAsync(ApiFactory factory, Guid tenantId, string email, string roleName, string[] permissions)
    {
        using var register = factory.CreateClient();
        (await register.PostAsJsonAsync("/api/v1/identity/register", new { email, password = "Correct-Horse-42" })).EnsureSuccessStatusCode();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        var userId = await db.Users.Where(x => x.Email == email).Select(x => x.Id).SingleAsync();
        var role = new TenantRole(tenantId, roleName, "seeded", false);
        foreach (var key in permissions) role.Permissions.Add(new TenantRolePermission(role.Id, key));
        var membership = new TenantUser(tenantId, userId, role.Id, DateTimeOffset.UtcNow);
        db.TenantRoles.Add(role);
        db.TenantUsers.Add(membership);
        await db.SaveChangesAsync();
        return new SeededMember(membership.Id, role.Id, userId);
    }

    private static async Task<Guid> CreateRoleAsync(HttpClient client, string name, string[] permissions)
    {
        var response = await client.PostAsJsonAsync("/api/v1/tenant/roles", new { name, description = "x", permissions });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<RoleRef>())!.Id;
    }

    private static async Task ReduceSelfToRoleAsync(HttpClient client, string global, string slug, string[] permissions)
    {
        var roleId = await CreateRoleAsync(client, "Reduzido", permissions);
        var members = await client.GetFromJsonAsync<IdRef[]>("/api/v1/tenant/members");
        var self = members!.First();
        (await client.PatchAsJsonAsync($"/api/v1/tenant/members/{self.Id}/role", new { roleId })).EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Authorization = await ScopedAsync(client, global, slug);
    }

    private static async Task<Guid> UserIdAsync(ApiFactory factory, string normalizedEmail)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<NexoraDbContext>()
            .Users.Where(x => x.NormalizedEmail == normalizedEmail).Select(x => x.Id).SingleAsync();
    }

    private static async Task<AuditRow> SingleAuditAsync(ApiFactory factory, string action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        return await db.AuditLogs.Where(x => x.Action == action)
            .Select(x => new AuditRow(x.ActorUserId, x.TenantId, x.Action, x.TargetType, x.TargetId, x.Details))
            .SingleAsync();
    }

    private static async Task<int> CountAuditAsync(ApiFactory factory, string action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<NexoraDbContext>().AuditLogs.CountAsync(x => x.Action == action);
    }

    private static async Task<(string Global, string Slug, Guid TenantId)> SetUpTenantAsync(HttpClient client, string email, string slug)
    {
        var global = await RegisterAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", global);
        var response = await client.PostAsJsonAsync("/api/v1/tenants", new { name = slug, slug, timeZoneId = "UTC" });
        response.EnsureSuccessStatusCode();
        return (global, slug, (await response.Content.ReadFromJsonAsync<TenantDto>())!.Id);
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
    private sealed record RoleRef(Guid Id, bool IsSystem);
    private sealed record AuditRow(Guid ActorUserId, Guid? TenantId, string Action, string TargetType, string TargetId, string? Details);
}
