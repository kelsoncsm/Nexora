using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Application.Administration;
using Nexora.Domain.Identity;
using Nexora.Infrastructure.Persistence;
using Nexora.Infrastructure.Administration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Nexora.IntegrationTests;

public sealed class PlatformAdministrationTests
{
    [Fact]
    public async Task PlatformAdminBootstrapIsIdempotent()
    {
        await using var factory = new ApiFactory(); var client = factory.CreateClient();
        await RegisterAsync(client, "bootstrap@nexora.test");
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["Administration:BootstrapAdminEmail"] = "bootstrap@nexora.test" }).Build();
        var bootstrapper = new PlatformAdminBootstrapper(db, configuration, NullLogger<PlatformAdminBootstrapper>.Instance);
        await bootstrapper.BootstrapAsync(CancellationToken.None);
        db.ChangeTracker.Clear();
        await bootstrapper.BootstrapAsync(CancellationToken.None);
        var role = await db.Roles.Include(x => x.Permissions).SingleAsync(x => x.Name == "PlatformAdmin");
        Assert.Equal(PlatformPermissions.All.Length, role.Permissions.Count);
    }

    [Fact]
    public async Task PlatformAdminIsAuthorizedRegularUserIsForbiddenAndMutationsAreAudited()
    {
        await using var factory = new ApiFactory();
        var regular = factory.CreateClient(); var admin = factory.CreateClient();
        var regularToken = await RegisterAsync(regular, "tenant-admin@nexora.test");
        await RegisterAsync(admin, "platform@nexora.test");
        regular.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", regularToken);
        var tenantResponse = await regular.PostAsJsonAsync("/api/v1/tenants", new { name = "Tenant A", slug = "tenant-a", timeZoneId = "UTC" });
        tenantResponse.EnsureSuccessStatusCode();
        var tenantId = (await tenantResponse.Content.ReadFromJsonAsync<TenantResponse>())!.Id;

        await PromoteAsync(factory, "PLATFORM@NEXORA.TEST");
        var adminToken = await LoginAsync(admin, "platform@nexora.test");
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.GetAsync("/api/v1/customers")).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await regular.GetAsync("/api/v1/admin/dashboard")).StatusCode);
        var dashboard = await admin.GetFromJsonAsync<DashboardResponse>("/api/v1/admin/dashboard");
        Assert.NotNull(dashboard); Assert.Equal(1, dashboard.TotalTenants);

        var status = await admin.PatchAsJsonAsync($"/api/v1/admin/tenants/{tenantId}/status", new { isActive = false });
        Assert.Equal(HttpStatusCode.NoContent, status.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await regular.GetAsync("/api/v1/t/tenant-a")).StatusCode);

        var audit = await admin.GetFromJsonAsync<AuditResponse[]>("/api/v1/admin/audit-logs");
        Assert.Contains(audit!, x => x.Action == "tenant.deactivate" && x.TargetId == tenantId.ToString());

        var segment = await admin.PostAsJsonAsync("/api/v1/admin/segments", new { code = "clinic", name = "Clínica" });
        Assert.Equal(HttpStatusCode.Created, segment.StatusCode);
        var updatedAudit = await admin.GetFromJsonAsync<AuditResponse[]>("/api/v1/admin/audit-logs");
        Assert.NotNull(updatedAudit); Assert.Contains(updatedAudit, x => x.Action == "segment.create");
    }

    private static async Task<string> RegisterAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/identity/register", new { email, password = "Correct-Horse-42" });
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken;
    }
    private static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/identity/login", new { email, password = "Correct-Horse-42" });
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken;
    }
    private static async Task PromoteAsync(ApiFactory factory, string normalizedEmail)
    {
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        var user = await db.Users.SingleAsync(x => x.NormalizedEmail == normalizedEmail);
        var role = new Role("PlatformAdmin"); var permission = new Permission(PlatformPermissions.Access);
        role.Permissions.Add(new RolePermission(role.Id, permission.Id)); user.Roles.Add(new UserRole(user.Id, role.Id));
        db.Roles.Add(role); db.Permissions.Add(permission); await db.SaveChangesAsync();
    }
    private sealed record TokenResponse(string AccessToken);
    private sealed record TenantResponse(Guid Id);
    private sealed record DashboardResponse(int TotalTenants);
    private sealed record AuditResponse(string Action, string TargetId);
}
