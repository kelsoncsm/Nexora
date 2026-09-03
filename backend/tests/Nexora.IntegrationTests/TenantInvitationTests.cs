using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Domain.Notifications;
using Nexora.Domain.Tenancy;
using Nexora.Infrastructure.Notifications;
using Nexora.Infrastructure.Persistence;

namespace Nexora.IntegrationTests;

/// <summary>
/// Tenant member invitation workflow (create / list / resend / cancel / accept). Locks the
/// behaviour Kelson signed off on 2026-09-02: the tenant is always the caller's session tenant
/// (never the request body), the raw token lives only in the outbox e-mail and is single-use,
/// every mutation is gated on the <c>tenant.members.*</c> CRUD permissions, cross-tenant access is
/// invisible (404), and the four audit events carry no secret material.
/// </summary>
public sealed class TenantInvitationTests
{
    private const string Password = "Correct-Horse-42";
    private const string InvitationTemplate = "TenantInvitationEmail";
    private const string Invitations = "/api/v1/tenant/members/invitations";

    private static readonly string[] MembersReadOnly = ["tenant.members.read"];
    private static readonly string[] MembersReadUpdate = ["tenant.members.read", "tenant.members.update"];
    private static readonly string[] MembersReadDelete = ["tenant.members.read", "tenant.members.delete"];
    private static readonly string[] CustomersReadOnly = ["customers.read"];

    // ---- CRIAÇÃO ---------------------------------------------------------------------------------

    [Fact]
    public async Task CreateWithMembersCreateIssuesPendingInvitation()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await OnboardAsync(client, "create-ok@nexora.test", "create-ok");
        var role = await CreateRoleAsync(client, "Recepcao", CustomersReadOnly);

        var response = await client.PostAsJsonAsync(Invitations, new { email = "invitee@nexora.test", roleId = role });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var view = await response.Content.ReadFromJsonAsync<InvitationView>();
        Assert.Equal("invitee@nexora.test", view!.Email);
        Assert.Equal("Pending", view.Status);
        Assert.Equal(role, view.RoleId);
        Assert.Null(view.AcceptedAt);
        Assert.False(string.IsNullOrWhiteSpace(await TokenFromOutboxAsync(factory, "invitee@nexora.test")));
    }

    [Fact]
    public async Task CreateWithoutMembersCreatePermissionIsForbidden()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var (_, slug, tenantId) = await OnboardAsync(client, "create-403@nexora.test", "create-403");
        var role = await CreateRoleAsync(client, "Recepcao", CustomersReadOnly);
        var reader = await SeedMemberAsync(factory, tenantId, "create-403-reader@nexora.test", "Reader", MembersReadOnly);
        await AuthenticateAsync(client, reader.Email, slug);

        var response = await client.PostAsJsonAsync(Invitations, new { email = "x@nexora.test", roleId = role });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateUnauthenticatedIsUnauthorized()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(Invitations, new { email = "x@nexora.test", roleId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateWithRoleThatDoesNotExistIsRejected()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await OnboardAsync(client, "create-badrole@nexora.test", "create-badrole");

        var response = await client.PostAsJsonAsync(Invitations, new { email = "x@nexora.test", roleId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateWithRoleFromAnotherTenantIsRejected()
    {
        await using var factory = new ApiFactory();
        using var a = factory.CreateClient();
        using var b = factory.CreateClient();
        await OnboardAsync(a, "create-iso-a@nexora.test", "create-iso-a");
        await OnboardAsync(b, "create-iso-b@nexora.test", "create-iso-b");
        var roleB = await CreateRoleAsync(b, "B-Only", CustomersReadOnly);

        var response = await a.PostAsJsonAsync(Invitations, new { email = "x@nexora.test", roleId = roleB });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateForSomeoneAlreadyAMemberIsRejected()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var (_, _, tenantId) = await OnboardAsync(client, "create-dupmember@nexora.test", "create-dupmember");
        var role = await CreateRoleAsync(client, "Recepcao", CustomersReadOnly);
        await SeedMemberAsync(factory, tenantId, "already-in@nexora.test", "Staff", CustomersReadOnly);

        var response = await client.PostAsJsonAsync(Invitations, new { email = "ALREADY-IN@nexora.test", roleId = role });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task CreateWhenAPendingInvitationAlreadyExistsIsRejected()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await OnboardAsync(client, "create-dup@nexora.test", "create-dup");
        var role = await CreateRoleAsync(client, "Recepcao", CustomersReadOnly);
        (await client.PostAsJsonAsync(Invitations, new { email = "dup@nexora.test", roleId = role })).EnsureSuccessStatusCode();

        var again = await client.PostAsJsonAsync(Invitations, new { email = "dup@nexora.test", roleId = role });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task CreateIgnoresAnyTenantIdInTheRequestBody()
    {
        await using var factory = new ApiFactory();
        using var attacker = factory.CreateClient();
        using var victim = factory.CreateClient();
        var (_, _, attackerTenant) = await OnboardAsync(attacker, "create-body-att@nexora.test", "create-body-att");
        var (_, _, victimTenant) = await OnboardAsync(victim, "create-body-vic@nexora.test", "create-body-vic");
        var role = await CreateRoleAsync(attacker, "Recepcao", CustomersReadOnly);

        var response = await attacker.PostAsJsonAsync(Invitations,
            new { email = "planted@nexora.test", roleId = role, tenantId = victimTenant });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        var invitation = await db.TenantInvitations.SingleAsync(x => x.Email == "planted@nexora.test");
        Assert.Equal(attackerTenant, invitation.TenantId);
        Assert.NotEqual(victimTenant, invitation.TenantId);
    }

    // ---- LISTAGEM -------------------------------------------------------------------------------

    [Fact]
    public async Task ListRequiresMembersRead()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var (_, slug, tenantId) = await OnboardAsync(client, "list-403@nexora.test", "list-403");
        var member = await SeedMemberAsync(factory, tenantId, "list-403-m@nexora.test", "NoRead", CustomersReadOnly);
        await AuthenticateAsync(client, member.Email, slug);

        var response = await client.GetAsync(Invitations);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListReturnsOnlyTheCurrentTenantsInvitations()
    {
        await using var factory = new ApiFactory();
        using var a = factory.CreateClient();
        using var b = factory.CreateClient();
        await OnboardAsync(a, "list-iso-a@nexora.test", "list-iso-a");
        await OnboardAsync(b, "list-iso-b@nexora.test", "list-iso-b");
        var roleA = await CreateRoleAsync(a, "RA", CustomersReadOnly);
        var roleB = await CreateRoleAsync(b, "RB", CustomersReadOnly);
        (await a.PostAsJsonAsync(Invitations, new { email = "for-a@nexora.test", roleId = roleA })).EnsureSuccessStatusCode();
        (await b.PostAsJsonAsync(Invitations, new { email = "for-b@nexora.test", roleId = roleB })).EnsureSuccessStatusCode();

        var listA = await a.GetFromJsonAsync<InvitationView[]>(Invitations);

        Assert.Equal("for-a@nexora.test", Assert.Single(listA!).Email);
    }

    // ---- REENVIO --------------------------------------------------------------------------------

    [Fact]
    public async Task ResendRotatesTheTokenAndTheOldOneStopsWorking()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await OnboardAsync(client, "resend-rotate@nexora.test", "resend-rotate");
        var role = await CreateRoleAsync(client, "Recepcao", CustomersReadOnly);
        var id = await CreateInvitationAsync(client, "rotate@nexora.test", role);
        var firstToken = await TokenFromOutboxAsync(factory, "rotate@nexora.test");

        (await client.PostAsync($"{Invitations}/{id}/resend", null)).EnsureSuccessStatusCode();
        var secondToken = await TokenFromOutboxAsync(factory, "rotate@nexora.test");

        Assert.NotEqual(firstToken, secondToken);
        Assert.Equal(HttpStatusCode.BadRequest, (await AcceptAsync(client, firstToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AcceptAsync(client, secondToken)).StatusCode);
    }

    [Fact]
    public async Task ResendRequiresMembersCreate()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var (_, slug, tenantId) = await OnboardAsync(client, "resend-403@nexora.test", "resend-403");
        var role = await CreateRoleAsync(client, "Recepcao", CustomersReadOnly);
        var id = await CreateInvitationAsync(client, "resend-403-i@nexora.test", role);
        var reader = await SeedMemberAsync(factory, tenantId, "resend-403-r@nexora.test", "Reader", MembersReadOnly);
        await AuthenticateAsync(client, reader.Email, slug);

        var response = await client.PostAsync($"{Invitations}/{id}/resend", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ResendACancelledInvitationIsRejected()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await OnboardAsync(client, "resend-cancelled@nexora.test", "resend-cancelled");
        var role = await CreateRoleAsync(client, "Recepcao", CustomersReadOnly);
        var id = await CreateInvitationAsync(client, "rc@nexora.test", role);
        (await client.DeleteAsync($"{Invitations}/{id}")).EnsureSuccessStatusCode();

        var response = await client.PostAsync($"{Invitations}/{id}/resend", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResendAnAcceptedInvitationIsRejected()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await OnboardAsync(client, "resend-accepted@nexora.test", "resend-accepted");
        var role = await CreateRoleAsync(client, "Recepcao", CustomersReadOnly);
        var id = await CreateInvitationAsync(client, "ra@nexora.test", role);
        (await AcceptAsync(client, await TokenFromOutboxAsync(factory, "ra@nexora.test"))).EnsureSuccessStatusCode();

        var response = await client.PostAsync($"{Invitations}/{id}/resend", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ResendPushesTheExpiryBackToTheFullWindow()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await OnboardAsync(client, "resend-expiry@nexora.test", "resend-expiry");
        var role = await CreateRoleAsync(client, "Recepcao", CustomersReadOnly);
        var id = await CreateInvitationAsync(client, "re@nexora.test", role);
        await ShiftExpiryAsync(factory, "re@nexora.test", DateTimeOffset.UtcNow.AddHours(2));

        var resent = await client.PostAsync($"{Invitations}/{id}/resend", null);
        resent.EnsureSuccessStatusCode();
        var view = await resent.Content.ReadFromJsonAsync<InvitationView>();

        var days = (view!.ExpiresAt - DateTimeOffset.UtcNow).TotalDays;
        Assert.InRange(days, 6.9, 7.1);
    }

    // ---- CANCELAMENTO -------------------------------------------------------------------------

    [Fact]
    public async Task CancelMarksTheInvitationCancelledAndKillsTheToken()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await OnboardAsync(client, "cancel-ok@nexora.test", "cancel-ok");
        var role = await CreateRoleAsync(client, "Recepcao", CustomersReadOnly);
        var id = await CreateInvitationAsync(client, "cancelled@nexora.test", role);
        var token = await TokenFromOutboxAsync(factory, "cancelled@nexora.test");

        var cancel = await client.DeleteAsync($"{Invitations}/{id}");

        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        var view = (await client.GetFromJsonAsync<InvitationView[]>(Invitations))!.Single();
        Assert.Equal("Cancelled", view.Status);
        Assert.NotNull(view.CancelledAt);
        Assert.Equal(HttpStatusCode.BadRequest, (await AcceptAsync(client, token)).StatusCode);
    }

    [Fact]
    public async Task CancelRequiresMembersDelete()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var (_, slug, tenantId) = await OnboardAsync(client, "cancel-403@nexora.test", "cancel-403");
        var role = await CreateRoleAsync(client, "Recepcao", CustomersReadOnly);
        var id = await CreateInvitationAsync(client, "cancel-403-i@nexora.test", role);
        var reader = await SeedMemberAsync(factory, tenantId, "cancel-403-r@nexora.test", "Reader", MembersReadOnly);
        await AuthenticateAsync(client, reader.Email, slug);

        var response = await client.DeleteAsync($"{Invitations}/{id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CancelAnAcceptedInvitationIsRejected()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await OnboardAsync(client, "cancel-accepted@nexora.test", "cancel-accepted");
        var role = await CreateRoleAsync(client, "Recepcao", CustomersReadOnly);
        var id = await CreateInvitationAsync(client, "ca@nexora.test", role);
        (await AcceptAsync(client, await TokenFromOutboxAsync(factory, "ca@nexora.test"))).EnsureSuccessStatusCode();

        var response = await client.DeleteAsync($"{Invitations}/{id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CancelAnotherTenantsInvitationIsNotFound()
    {
        await using var factory = new ApiFactory();
        using var a = factory.CreateClient();
        using var b = factory.CreateClient();
        await OnboardAsync(a, "cancel-iso-a@nexora.test", "cancel-iso-a");
        await OnboardAsync(b, "cancel-iso-b@nexora.test", "cancel-iso-b");
        var roleB = await CreateRoleAsync(b, "RB", CustomersReadOnly);
        var idB = await CreateInvitationAsync(b, "victim@nexora.test", roleB);

        var response = await a.DeleteAsync($"{Invitations}/{idB}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        Assert.Equal(TenantInvitationStatus.Pending, (await db.TenantInvitations.SingleAsync(x => x.Id == idB)).Status);
    }

    [Fact]
    public async Task ResendAnotherTenantsInvitationIsNotFound()
    {
        await using var factory = new ApiFactory();
        using var a = factory.CreateClient();
        using var b = factory.CreateClient();
        await OnboardAsync(a, "resend-iso-a@nexora.test", "resend-iso-a");
        await OnboardAsync(b, "resend-iso-b@nexora.test", "resend-iso-b");
        var roleB = await CreateRoleAsync(b, "RB", CustomersReadOnly);
        var idB = await CreateInvitationAsync(b, "victim2@nexora.test", roleB);

        var response = await a.PostAsync($"{Invitations}/{idB}/resend", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- ACEITE — USUÁRIO NOVO ---------------------------------------------------------------

    [Fact]
    public async Task AcceptNewUserCreatesTheAccountBoundToTheInvitedTenantAndRole()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var (_, _, tenantId) = await OnboardAsync(client, "accept-new@nexora.test", "accept-new");
        var role = await CreateRoleAsync(client, "Recepcao", CustomersReadOnly);
        var id = await CreateInvitationAsync(client, "newbie@nexora.test", role);
        var token = await TokenFromOutboxAsync(factory, "newbie@nexora.test");

        var response = await AcceptAsync(client, token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<AcceptResult>();
        Assert.True(result!.AccountCreated);
        Assert.Equal(tenantId, result.TenantId);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        var user = await db.Users.SingleAsync(x => x.NormalizedEmail == "NEWBIE@NEXORA.TEST");
        var membership = await db.TenantUsers.SingleAsync(x => x.UserId == user.Id);
        Assert.Equal(tenantId, membership.TenantId);
        Assert.Equal(role, membership.TenantRoleId);
        Assert.True(membership.IsActive);
        var invitation = await db.TenantInvitations.SingleAsync(x => x.Id == id);
        Assert.Equal(TenantInvitationStatus.Accepted, invitation.Status);
        Assert.Equal(user.Id, invitation.AcceptedByUserId);
        Assert.NotNull(invitation.AcceptedAt);
    }

    [Fact]
    public async Task AcceptNewUserStoresACredentialTheOfficialLoginAccepts()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await OnboardAsync(client, "accept-hash@nexora.test", "accept-hash");
        var role = await CreateRoleAsync(client, "Recepcao", CustomersReadOnly);
        await CreateInvitationAsync(client, "hashed@nexora.test", role);
        var token = await TokenFromOutboxAsync(factory, "hashed@nexora.test");
        (await AcceptAsync(client, token)).EnsureSuccessStatusCode();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        var hash = await db.Users.Where(x => x.NormalizedEmail == "HASHED@NEXORA.TEST").Select(x => x.PasswordHash).SingleAsync();
        Assert.False(string.IsNullOrEmpty(hash));
        Assert.NotEqual(Password, hash);

        using var fresh = factory.CreateClient();
        var login = await fresh.PostAsJsonAsync("/api/v1/identity/login", new { email = "hashed@nexora.test", password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task AcceptReusingAConsumedTokenIsRejected()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await OnboardAsync(client, "accept-reuse@nexora.test", "accept-reuse");
        var role = await CreateRoleAsync(client, "Recepcao", CustomersReadOnly);
        await CreateInvitationAsync(client, "reuse@nexora.test", role);
        var token = await TokenFromOutboxAsync(factory, "reuse@nexora.test");
        (await AcceptAsync(client, token)).EnsureSuccessStatusCode();

        var second = await AcceptAsync(client, token);

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task AcceptNewUserWithAWeakPasswordIsRejected()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await OnboardAsync(client, "accept-weak@nexora.test", "accept-weak");
        var role = await CreateRoleAsync(client, "Recepcao", CustomersReadOnly);
        await CreateInvitationAsync(client, "weak@nexora.test", role);
        var token = await TokenFromOutboxAsync(factory, "weak@nexora.test");

        var response = await client.PostAsJsonAsync($"{Invitations}/accept",
            new { token, password = "short", passwordConfirmation = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AcceptNewUserWithMismatchedConfirmationIsRejected()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await OnboardAsync(client, "accept-mismatch@nexora.test", "accept-mismatch");
        var role = await CreateRoleAsync(client, "Recepcao", CustomersReadOnly);
        await CreateInvitationAsync(client, "mismatch@nexora.test", role);
        var token = await TokenFromOutboxAsync(factory, "mismatch@nexora.test");

        var response = await client.PostAsJsonAsync($"{Invitations}/accept",
            new { token, password = Password, passwordConfirmation = "Different-Horse-99" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AcceptIgnoresTenantAndRoleOverridesSmuggledInTheBody()
    {
        await using var factory = new ApiFactory();
        using var host = factory.CreateClient();
        using var attacker = factory.CreateClient();
        var (_, _, hostTenant) = await OnboardAsync(host, "accept-body-host@nexora.test", "accept-body-host");
        var (_, _, attackerTenant) = await OnboardAsync(attacker, "accept-body-att@nexora.test", "accept-body-att");
        var invitedRole = await CreateRoleAsync(host, "Recepcao", CustomersReadOnly);
        var attackerRole = await CreateRoleAsync(attacker, "Boss", CustomersReadOnly);
        await CreateInvitationAsync(host, "smuggler@nexora.test", invitedRole);
        var token = await TokenFromOutboxAsync(factory, "smuggler@nexora.test");

        using var raw = factory.CreateClient();
        var response = await raw.PostAsJsonAsync($"{Invitations}/accept", new
        {
            token,
            password = Password,
            passwordConfirmation = Password,
            tenantId = attackerTenant,
            tenantRoleId = attackerRole,
        });
        response.EnsureSuccessStatusCode();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        var user = await db.Users.SingleAsync(x => x.NormalizedEmail == "SMUGGLER@NEXORA.TEST");
        var membership = await db.TenantUsers.SingleAsync(x => x.UserId == user.Id);
        Assert.Equal(hostTenant, membership.TenantId);
        Assert.Equal(invitedRole, membership.TenantRoleId);
        Assert.NotEqual(attackerTenant, membership.TenantId);
    }

    // ---- ACEITE — USUÁRIO EXISTENTE -------------------------------------------------------

    [Fact]
    public async Task AcceptExistingUserAddsOnlyAMembershipAndNoSecondAccount()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var (_, _, tenantId) = await OnboardAsync(client, "accept-existing@nexora.test", "accept-existing");
        var role = await CreateRoleAsync(client, "Recepcao", CustomersReadOnly);
        using var invitee = factory.CreateClient();
        (await invitee.PostAsJsonAsync("/api/v1/identity/register",
            new { email = "veteran@nexora.test", password = Password })).EnsureSuccessStatusCode();
        await CreateInvitationAsync(client, "veteran@nexora.test", role);
        var token = await TokenFromOutboxAsync(factory, "veteran@nexora.test");

        var response = await AcceptAsync(client, token);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<AcceptResult>();
        Assert.False(result!.AccountCreated);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        Assert.Equal(1, await db.Users.CountAsync(x => x.NormalizedEmail == "VETERAN@NEXORA.TEST"));
        var user = await db.Users.SingleAsync(x => x.NormalizedEmail == "VETERAN@NEXORA.TEST");
        var membership = await db.TenantUsers.SingleAsync(x => x.UserId == user.Id && x.TenantId == tenantId);
        Assert.Equal(role, membership.TenantRoleId);
    }

    [Fact]
    public async Task AcceptExistingUserWithTheWrongPasswordIsUnauthorized()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await OnboardAsync(client, "accept-badpwd@nexora.test", "accept-badpwd");
        var role = await CreateRoleAsync(client, "Recepcao", CustomersReadOnly);
        using var invitee = factory.CreateClient();
        (await invitee.PostAsJsonAsync("/api/v1/identity/register",
            new { email = "wrongpwd@nexora.test", password = Password })).EnsureSuccessStatusCode();
        await CreateInvitationAsync(client, "wrongpwd@nexora.test", role);
        var token = await TokenFromOutboxAsync(factory, "wrongpwd@nexora.test");

        using var raw = factory.CreateClient();
        var response = await raw.PostAsJsonAsync($"{Invitations}/accept",
            new { token, password = "Not-The-Right-Password-1", passwordConfirmation = (string?)null });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AcceptExistingUserCannotBeAcceptedByADifferentSignedInUser()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await OnboardAsync(client, "accept-foreign@nexora.test", "accept-foreign");
        var role = await CreateRoleAsync(client, "Recepcao", CustomersReadOnly);
        using var target = factory.CreateClient();
        (await target.PostAsJsonAsync("/api/v1/identity/register",
            new { email = "target@nexora.test", password = Password })).EnsureSuccessStatusCode();
        await CreateInvitationAsync(client, "target@nexora.test", role);
        var token = await TokenFromOutboxAsync(factory, "target@nexora.test");

        using var actor = factory.CreateClient();
        var actorToken = (await (await actor.PostAsJsonAsync("/api/v1/identity/register",
            new { email = "actor@nexora.test", password = "Actor-Horse-Battery-7" })).Content.ReadFromJsonAsync<Token>())!.AccessToken;
        actor.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", actorToken);

        var response = await actor.PostAsJsonAsync($"{Invitations}/accept",
            new { token, password = "Actor-Horse-Battery-7", passwordConfirmation = (string?)null });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        Assert.Equal(TenantInvitationStatus.Pending, (await db.TenantInvitations.SingleAsync(x => x.Email == "target@nexora.test")).Status);
    }

    [Fact]
    public async Task AcceptExistingDeactivatedMemberIsReactivatedWithoutDuplicateMembership()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var (_, _, tenantId) = await OnboardAsync(client, "accept-react@nexora.test", "accept-react");
        var role = await CreateRoleAsync(client, "Recepcao", CustomersReadOnly);
        var member = await SeedMemberAsync(factory, tenantId, "returning@nexora.test", "Old", CustomersReadOnly);
        (await client.PatchAsync($"/api/v1/tenant/members/{member.MembershipId}/deactivate", null)).EnsureSuccessStatusCode();
        await CreateInvitationAsync(client, "returning@nexora.test", role);
        var token = await TokenFromOutboxAsync(factory, "returning@nexora.test");

        (await AcceptAsync(client, token)).EnsureSuccessStatusCode();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        var memberships = await db.TenantUsers.Where(x => x.UserId == member.UserId && x.TenantId == tenantId).ToListAsync();
        var single = Assert.Single(memberships);
        Assert.True(single.IsActive);
        Assert.Equal(role, single.TenantRoleId);
    }

    // ---- TOKEN --------------------------------------------------------------------------------

    [Theory]
    [InlineData("")]
    [InlineData("not-a-real-token")]
    [InlineData("!!!definitely not base64!!!")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task AcceptWithAnInvalidOrMalformedTokenIsRejected(string token)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();

        var response = await AcceptAsync(client, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AcceptWithAnExpiredTokenIsRejected()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await OnboardAsync(client, "token-expired@nexora.test", "token-expired");
        var role = await CreateRoleAsync(client, "Recepcao", CustomersReadOnly);
        await CreateInvitationAsync(client, "expired@nexora.test", role);
        var token = await TokenFromOutboxAsync(factory, "expired@nexora.test");
        await ShiftExpiryAsync(factory, "expired@nexora.test", DateTimeOffset.UtcNow.AddDays(-1));

        var response = await AcceptAsync(client, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task TokensNeverAppearInAuditDetails()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await OnboardAsync(client, "token-audit@nexora.test", "token-audit");
        var role = await CreateRoleAsync(client, "Recepcao", CustomersReadOnly);
        var id = await CreateInvitationAsync(client, "tokaudit@nexora.test", role);
        var token = await TokenFromOutboxAsync(factory, "tokaudit@nexora.test");
        (await client.PostAsync($"{Invitations}/{id}/resend", null)).EnsureSuccessStatusCode();
        var rotated = await TokenFromOutboxAsync(factory, "tokaudit@nexora.test");
        (await AcceptAsync(client, rotated)).EnsureSuccessStatusCode();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        var details = await db.AuditLogs
            .Where(x => x.Action.StartsWith("tenant_member") || x.Action.StartsWith("tenant_invitation"))
            .Select(x => x.Details)
            .ToListAsync();

        Assert.NotEmpty(details);
        foreach (var payload in details)
        {
            Assert.NotNull(payload);
            Assert.DoesNotContain(token, payload);
            Assert.DoesNotContain(rotated, payload);
            Assert.DoesNotContain(HashToken(token), payload, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(HashToken(rotated), payload, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", payload, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(Password, payload);
            Assert.DoesNotContain("convite/aceitar", payload);
        }
    }

    [Fact]
    public async Task TokensNeverAppearInApiResponses()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await OnboardAsync(client, "token-resp@nexora.test", "token-resp");
        var role = await CreateRoleAsync(client, "Recepcao", CustomersReadOnly);

        var created = await client.PostAsJsonAsync(Invitations, new { email = "tokresp@nexora.test", roleId = role });
        var createdBody = await created.Content.ReadAsStringAsync();
        var listBody = await (await client.GetAsync(Invitations)).Content.ReadAsStringAsync();
        var token = await TokenFromOutboxAsync(factory, "tokresp@nexora.test");
        var acceptBody = await (await AcceptAsync(client, token)).Content.ReadAsStringAsync();

        foreach (var body in new[] { createdBody, listBody, acceptBody })
        {
            Assert.DoesNotContain(token, body);
            Assert.DoesNotContain(HashToken(token), body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"token\"", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("tokenHash", body, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("acceptUrl", body, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ---- ISOLAMENTO / RBAC ------------------------------------------------------------------

    [Fact]
    public async Task ARoleFromAnotherTenantCannotBeAssignedThroughAnInvitation()
    {
        await using var factory = new ApiFactory();
        using var a = factory.CreateClient();
        using var b = factory.CreateClient();
        await OnboardAsync(a, "iso-role-a@nexora.test", "iso-role-a");
        await OnboardAsync(b, "iso-role-b@nexora.test", "iso-role-b");
        var roleB = await CreateRoleAsync(b, "B-Only", CustomersReadOnly);

        var response = await a.PostAsJsonAsync(Invitations, new { email = "cross@nexora.test", roleId = roleB });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReadEndpointsEnforceMembersRead()
    {
        await using var factory = new ApiFactory();
        using var owner = factory.CreateClient();
        var (_, slug, tenantId) = await OnboardAsync(owner, "rbac-read@nexora.test", "rbac-read");

        using var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync(Invitations)).StatusCode);

        using var reader = factory.CreateClient();
        var noRead = await SeedMemberAsync(factory, tenantId, "rbac-read-x@nexora.test", "NoRead", CustomersReadOnly);
        await AuthenticateAsync(reader, noRead.Email, slug);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync(Invitations)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync("/api/v1/tenant/members/assignable-roles")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync(Invitations)).StatusCode);
    }

    [Fact]
    public async Task MemberRoleAssignmentEnforcesMembersUpdate()
    {
        await using var factory = new ApiFactory();
        using var owner = factory.CreateClient();
        var (_, slug, tenantId) = await OnboardAsync(owner, "rbac-update@nexora.test", "rbac-update");
        var target = await SeedMemberAsync(factory, tenantId, "rbac-update-t@nexora.test", "Target", CustomersReadOnly);
        // A role whose permissions are a subset of the updater's own — otherwise the no-escalation
        // rule (below) would 403 even a permission-holder.
        var newRole = await CreateRoleAsync(owner, "Gerente", MembersReadOnly);

        using var anon = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anon.PatchAsJsonAsync($"/api/v1/tenant/members/{target.MembershipId}/role", new { roleId = newRole })).StatusCode);

        using var reader = factory.CreateClient();
        var noUpdate = await SeedMemberAsync(factory, tenantId, "rbac-update-r@nexora.test", "Reader", MembersReadOnly);
        await AuthenticateAsync(reader, noUpdate.Email, slug);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await reader.PatchAsJsonAsync($"/api/v1/tenant/members/{target.MembershipId}/role", new { roleId = newRole })).StatusCode);

        using var updater = factory.CreateClient();
        var canUpdate = await SeedMemberAsync(factory, tenantId, "rbac-update-u@nexora.test", "Updater", MembersReadUpdate);
        await AuthenticateAsync(updater, canUpdate.Email, slug);
        Assert.Equal(HttpStatusCode.NoContent,
            (await updater.PatchAsJsonAsync($"/api/v1/tenant/members/{target.MembershipId}/role", new { roleId = newRole })).StatusCode);
    }

    // ---- NO PRIVILEGE ESCALATION (architectural decision 2026-09-02) -----------------------
    // RolePermissions ⊆ ActorPermissions for every assign / invite / assignable-roles call.
    // No exception for tenant.manage or for system roles.

    private static readonly string[] MembersReadUpdateCustomers =
        ["tenant.members.read", "tenant.members.update", "customers.read", "customers.create"];

    [Fact]
    public async Task MemberCannotAssignARoleAboveTheirOwnPrivileges()
    {
        await using var factory = new ApiFactory();
        using var owner = factory.CreateClient();
        var (_, slug, tenantId) = await OnboardAsync(owner, "esc-assign@nexora.test", "esc-assign");
        var target = await SeedMemberAsync(factory, tenantId, "esc-assign-t@nexora.test", "Target", MembersReadOnly);
        // Actor holds members.update but NOT customers.* — cannot hand out customers.read.
        var actor = await SeedMemberAsync(factory, tenantId, "esc-assign-a@nexora.test", "Editor", MembersReadUpdate);
        var over = await CreateRoleAsync(owner, "Atendimento", CustomersReadOnly);

        using var client = factory.CreateClient();
        await AuthenticateAsync(client, actor.Email, slug);
        var response = await client.PatchAsJsonAsync($"/api/v1/tenant/members/{target.MembershipId}/role", new { roleId = over });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        Assert.Equal(target.RoleId, (await db.TenantUsers.SingleAsync(x => x.Id == target.MembershipId)).TenantRoleId);
    }

    [Fact]
    public async Task MemberCannotInviteToARoleAboveTheirOwnPrivileges()
    {
        await using var factory = new ApiFactory();
        using var owner = factory.CreateClient();
        var (_, slug, tenantId) = await OnboardAsync(owner, "esc-invite@nexora.test", "esc-invite");
        var actor = await SeedMemberAsync(factory, tenantId, "esc-invite-a@nexora.test", "Recruiter",
            ["tenant.members.read", "tenant.members.create"]);
        var over = await CreateRoleAsync(owner, "Atendimento", CustomersReadOnly);

        using var client = factory.CreateClient();
        await AuthenticateAsync(client, actor.Email, slug);
        var response = await client.PostAsJsonAsync(Invitations, new { email = "esc-target@nexora.test", roleId = over });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminCannotInviteToTheSystemAdminRoleUnlessTheyHoldEveryPermission()
    {
        await using var factory = new ApiFactory();
        using var owner = factory.CreateClient();
        var (_, slug, tenantId) = await OnboardAsync(owner, "esc-sysadmin@nexora.test", "esc-sysadmin");
        // members.create holder — nowhere near the full permission set the ADMIN role carries.
        var actor = await SeedMemberAsync(factory, tenantId, "esc-sysadmin-a@nexora.test", "Recruiter",
            ["tenant.members.read", "tenant.members.create"]);
        var adminRoleId = (await owner.GetFromJsonAsync<RoleRef[]>("/api/v1/tenant/roles"))!.Single(x => x.IsSystem).Id;

        using var client = factory.CreateClient();
        await AuthenticateAsync(client, actor.Email, slug);
        var response = await client.PostAsJsonAsync(Invitations, new { email = "esc-sysadmin-t@nexora.test", roleId = adminRoleId });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AssignableRolesOmitsRolesAboveTheActorsPrivileges()
    {
        await using var factory = new ApiFactory();
        using var owner = factory.CreateClient();
        var (_, slug, tenantId) = await OnboardAsync(owner, "esc-list@nexora.test", "esc-list");
        var withinReach = await CreateRoleAsync(owner, "Somente Leitura", MembersReadOnly);
        var outOfReach = await CreateRoleAsync(owner, "Atendimento", CustomersReadOnly);
        var actor = await SeedMemberAsync(factory, tenantId, "esc-list-a@nexora.test", "Recruiter",
            ["tenant.members.read", "tenant.members.create"]);

        using var client = factory.CreateClient();
        await AuthenticateAsync(client, actor.Email, slug);
        var roles = await client.GetFromJsonAsync<AssignableRole[]>("/api/v1/tenant/members/assignable-roles");

        var ids = roles!.Select(r => r.Id).ToHashSet();
        Assert.Contains(withinReach, ids);
        Assert.DoesNotContain(outOfReach, ids);
        Assert.DoesNotContain(roles!, r => r.IsSystem); // the ADMIN role is never within a recruiter's reach
    }

    [Fact]
    public async Task ARoleWhosePermissionsEqualTheActorsCanBeAssigned()
    {
        await using var factory = new ApiFactory();
        using var owner = factory.CreateClient();
        var (_, slug, tenantId) = await OnboardAsync(owner, "esc-equal@nexora.test", "esc-equal");
        var target = await SeedMemberAsync(factory, tenantId, "esc-equal-t@nexora.test", "Target", MembersReadOnly);
        var actor = await SeedMemberAsync(factory, tenantId, "esc-equal-a@nexora.test", "Editor", MembersReadUpdateCustomers);
        var equalRole = await CreateRoleAsync(owner, "Espelho", MembersReadUpdateCustomers);

        using var client = factory.CreateClient();
        await AuthenticateAsync(client, actor.Email, slug);
        var response = await client.PatchAsJsonAsync($"/api/v1/tenant/members/{target.MembershipId}/role", new { roleId = equalRole });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task ARoleThatIsASubsetOfTheActorsCanBeAssigned()
    {
        await using var factory = new ApiFactory();
        using var owner = factory.CreateClient();
        var (_, slug, tenantId) = await OnboardAsync(owner, "esc-subset@nexora.test", "esc-subset");
        var target = await SeedMemberAsync(factory, tenantId, "esc-subset-t@nexora.test", "Target", MembersReadOnly);
        var actor = await SeedMemberAsync(factory, tenantId, "esc-subset-a@nexora.test", "Editor", MembersReadUpdateCustomers);
        var subsetRole = await CreateRoleAsync(owner, "Atendimento", CustomersReadOnly);

        using var client = factory.CreateClient();
        await AuthenticateAsync(client, actor.Email, slug);
        var response = await client.PatchAsJsonAsync($"/api/v1/tenant/members/{target.MembershipId}/role", new { roleId = subsetRole });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task AdminCanAssignTheAdminRoleBecauseItHoldsEveryPermission()
    {
        await using var factory = new ApiFactory();
        using var owner = factory.CreateClient();
        var (_, _, tenantId) = await OnboardAsync(owner, "esc-admin@nexora.test", "esc-admin");
        var target = await SeedMemberAsync(factory, tenantId, "esc-admin-t@nexora.test", "Target", CustomersReadOnly);
        var adminRoleId = (await owner.GetFromJsonAsync<RoleRef[]>("/api/v1/tenant/roles"))!.Single(x => x.IsSystem).Id;

        var response = await owner.PatchAsJsonAsync($"/api/v1/tenant/members/{target.MembershipId}/role", new { roleId = adminRoleId });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private sealed record AssignableRole(Guid Id, string Name, bool IsSystem);

    [Fact]
    public async Task DeactivationEnforcesMembersDelete()
    {
        await using var factory = new ApiFactory();
        using var owner = factory.CreateClient();
        var (_, slug, tenantId) = await OnboardAsync(owner, "rbac-delete@nexora.test", "rbac-delete");
        var target = await SeedMemberAsync(factory, tenantId, "rbac-delete-t@nexora.test", "Target", CustomersReadOnly);

        using var reader = factory.CreateClient();
        var noDelete = await SeedMemberAsync(factory, tenantId, "rbac-delete-r@nexora.test", "Reader", MembersReadOnly);
        await AuthenticateAsync(reader, noDelete.Email, slug);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await reader.PatchAsync($"/api/v1/tenant/members/{target.MembershipId}/deactivate", null)).StatusCode);

        using var deleter = factory.CreateClient();
        var canDelete = await SeedMemberAsync(factory, tenantId, "rbac-delete-d@nexora.test", "Deleter", MembersReadDelete);
        await AuthenticateAsync(deleter, canDelete.Email, slug);
        Assert.Equal(HttpStatusCode.NoContent,
            (await deleter.PatchAsync($"/api/v1/tenant/members/{target.MembershipId}/deactivate", null)).StatusCode);
    }

    // ---- AUDITORIA -------------------------------------------------------------------------

    [Fact]
    public async Task AllFourInvitationEventsAreRecordedForTheTenantAndActor()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var (_, _, tenantId) = await OnboardAsync(client, "audit-inv@nexora.test", "audit-inv");
        var actorId = await UserIdAsync(factory, "AUDIT-INV@NEXORA.TEST");
        var role = await CreateRoleAsync(client, "Recepcao", CustomersReadOnly);

        var invited = await CreateInvitationAsync(client, "audited@nexora.test", role);
        (await client.PostAsync($"{Invitations}/{invited}/resend", null)).EnsureSuccessStatusCode();
        var cancelledId = await CreateInvitationAsync(client, "audited-cancel@nexora.test", role);
        (await client.DeleteAsync($"{Invitations}/{cancelledId}")).EnsureSuccessStatusCode();
        (await AcceptAsync(client, await TokenFromOutboxAsync(factory, "audited@nexora.test"))).EnsureSuccessStatusCode();

        var invitedEvts = await AuditRowsAsync(factory, "tenant_member.invited");
        Assert.Equal(2, invitedEvts.Count);
        Assert.All(invitedEvts, e =>
        {
            Assert.Equal(actorId, e.ActorUserId);
            Assert.Equal(tenantId, e.TenantId);
            Assert.Equal("TenantInvitation", e.TargetType);
        });
        Assert.Contains(invitedEvts, e => e.TargetId == invited.ToString());

        var resent = Assert.Single(await AuditRowsAsync(factory, "tenant_invitation.resent"));
        Assert.Equal(tenantId, resent.TenantId);
        var cancelled = Assert.Single(await AuditRowsAsync(factory, "tenant_invitation.cancelled"));
        Assert.Equal(tenantId, cancelled.TenantId);
        Assert.Equal(cancelledId.ToString(), cancelled.TargetId);

        var acceptedEvt = Assert.Single(await AuditRowsAsync(factory, "tenant_invitation.accepted"));
        Assert.Equal(tenantId, acceptedEvt.TenantId);
        Assert.NotEqual(actorId, acceptedEvt.ActorUserId);
    }

    // ====================================================================================
    // GAP COVERAGE (2026-09-03) — cases from the mandatory invitation-flow test matrix that
    // the suite above did not yet lock. Grouped by the matrix section they belong to.
    // ====================================================================================

    // ---- CRIAÇÃO -------------------------------------------------------------------------------

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateWithAMalformedEmailIsRejected(string email)
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await OnboardAsync(client, "create-bademail@nexora.test", "create-bademail");
        var role = await CreateRoleAsync(client, "Recepcao", CustomersReadOnly);

        var response = await client.PostAsJsonAsync(Invitations, new { email, roleId = role });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateReissuesAnExpiredButStillPendingInvitationInTheSameRow()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await OnboardAsync(client, "create-reissue@nexora.test", "create-reissue");
        var role = await CreateRoleAsync(client, "Recepcao", CustomersReadOnly);
        var id = await CreateInvitationAsync(client, "reissue@nexora.test", role);
        var staleToken = await TokenFromOutboxAsync(factory, "reissue@nexora.test");
        await ShiftExpiryAsync(factory, "reissue@nexora.test", DateTimeOffset.UtcNow.AddDays(-1));

        var again = await client.PostAsJsonAsync(Invitations, new { email = "reissue@nexora.test", roleId = role });

        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
        var view = await again.Content.ReadFromJsonAsync<InvitationView>();
        Assert.Equal(id, view!.Id);                       // same (tenant,email) slot re-used
        Assert.Equal("Pending", view.Status);
        Assert.InRange((view.ExpiresAt - DateTimeOffset.UtcNow).TotalDays, 6.9, 7.1);

        var freshToken = await TokenFromOutboxAsync(factory, "reissue@nexora.test");
        Assert.NotEqual(staleToken, freshToken);
        Assert.Equal(HttpStatusCode.BadRequest, (await AcceptAsync(client, staleToken)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AcceptAsync(client, freshToken)).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        Assert.Equal(1, await db.TenantInvitations.CountAsync(x => x.NormalizedEmail == "REISSUE@NEXORA.TEST"));
    }

    [Fact]
    public async Task CreateWithMembersCreatePermissionAloneIssuesTheInvitation()
    {
        await using var factory = new ApiFactory();
        using var owner = factory.CreateClient();
        var (_, slug, tenantId) = await OnboardAsync(owner, "create-min@nexora.test", "create-min");
        var subsetRole = await CreateRoleAsync(owner, "Recepcao", CustomersReadOnly);
        // Exactly read + create, plus the permission the target role grants (so the no-escalation
        // rule is satisfied and only the RBAC gate is under test).
        var actor = await SeedMemberAsync(factory, tenantId, "create-min-a@nexora.test", "Recruiter",
            ["tenant.members.read", "tenant.members.create", "customers.read"]);

        using var client = factory.CreateClient();
        await AuthenticateAsync(client, actor.Email, slug);
        var response = await client.PostAsJsonAsync(Invitations, new { email = "create-min-i@nexora.test", roleId = subsetRole });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // ---- REENVIO -----------------------------------------------------------------------------

    [Fact]
    public async Task ResendRevivesAnExpiredButStillPendingInvitation()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await OnboardAsync(client, "resend-expired@nexora.test", "resend-expired");
        var role = await CreateRoleAsync(client, "Recepcao", CustomersReadOnly);
        var id = await CreateInvitationAsync(client, "resexp@nexora.test", role);
        await ShiftExpiryAsync(factory, "resexp@nexora.test", DateTimeOffset.UtcNow.AddDays(-3));

        var resent = await client.PostAsync($"{Invitations}/{id}/resend", null);

        Assert.Equal(HttpStatusCode.OK, resent.StatusCode);
        var view = await resent.Content.ReadFromJsonAsync<InvitationView>();
        Assert.Equal("Pending", view!.Status);
        Assert.InRange((view.ExpiresAt - DateTimeOffset.UtcNow).TotalDays, 6.9, 7.1);
        Assert.Equal(HttpStatusCode.OK, (await AcceptAsync(client, await TokenFromOutboxAsync(factory, "resexp@nexora.test"))).StatusCode);
    }

    // ---- CANCELAMENTO ----------------------------------------------------------------------

    [Fact]
    public async Task CancelAnAlreadyCancelledInvitationIsRejected()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await OnboardAsync(client, "cancel-twice@nexora.test", "cancel-twice");
        var role = await CreateRoleAsync(client, "Recepcao", CustomersReadOnly);
        var id = await CreateInvitationAsync(client, "ct@nexora.test", role);
        (await client.DeleteAsync($"{Invitations}/{id}")).EnsureSuccessStatusCode();

        var second = await client.DeleteAsync($"{Invitations}/{id}");

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    // ---- ACEITE — GUARDAS DE ESTADO ------------------------------------------------------

    [Fact]
    public async Task AcceptIsRejectedWhenTheInvitedTenantIsInactive()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var (_, _, tenantId) = await OnboardAsync(client, "accept-inactive@nexora.test", "accept-inactive");
        var role = await CreateRoleAsync(client, "Recepcao", CustomersReadOnly);
        await CreateInvitationAsync(client, "inact@nexora.test", role);
        var token = await TokenFromOutboxAsync(factory, "inact@nexora.test");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
            (await db.Tenants.SingleAsync(x => x.Id == tenantId)).Deactivate();
            await db.SaveChangesAsync();
        }

        // Accept is anonymous — a fresh client with no stale tenant-session bearer.
        using var anon = factory.CreateClient();
        var response = await AcceptAsync(anon, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await using var verify = factory.Services.CreateAsyncScope();
        var vdb = verify.ServiceProvider.GetRequiredService<NexoraDbContext>();
        Assert.False(await vdb.Users.AnyAsync(x => x.NormalizedEmail == "INACT@NEXORA.TEST"));
        Assert.Equal(TenantInvitationStatus.Pending, (await vdb.TenantInvitations.SingleAsync(x => x.NormalizedEmail == "INACT@NEXORA.TEST")).Status);
    }

    [Fact]
    public async Task AcceptIsRejectedWhenTheInvitationRoleNoLongerExists()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await OnboardAsync(client, "accept-norole@nexora.test", "accept-norole");
        var role = await CreateRoleAsync(client, "Temporaria", CustomersReadOnly);
        await CreateInvitationAsync(client, "norole@nexora.test", role);
        var token = await TokenFromOutboxAsync(factory, "norole@nexora.test");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
            db.TenantRoles.Remove(await db.TenantRoles.SingleAsync(x => x.Id == role));
            await db.SaveChangesAsync();
        }

        var response = await AcceptAsync(client, token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- ISOLAMENTO ----------------------------------------------------------------------

    [Fact]
    public async Task TheSameEmailInvitedByTwoTenantsIsAcceptedIndependentlyPerTenant()
    {
        await using var factory = new ApiFactory();
        using var a = factory.CreateClient();
        using var b = factory.CreateClient();
        var (_, _, tenantA) = await OnboardAsync(a, "multi-a@nexora.test", "multi-a");
        var (_, _, tenantB) = await OnboardAsync(b, "multi-b@nexora.test", "multi-b");
        var roleA = await CreateRoleAsync(a, "RA", CustomersReadOnly);
        var roleB = await CreateRoleAsync(b, "RB", CustomersReadOnly);
        await CreateInvitationAsync(a, "shared@nexora.test", roleA);
        await CreateInvitationAsync(b, "shared@nexora.test", roleB);

        var tokenA = await TokenFromOutboxAsync(factory, "shared@nexora.test", tenantA);
        var tokenB = await TokenFromOutboxAsync(factory, "shared@nexora.test", tenantB);
        (await AcceptAsync(a, tokenA)).EnsureSuccessStatusCode();   // creates the account
        (await AcceptAsync(b, tokenB)).EnsureSuccessStatusCode();   // now an existing user, password matches

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        var user = Assert.Single(await db.Users.Where(x => x.NormalizedEmail == "SHARED@NEXORA.TEST").ToListAsync());
        var memberships = await db.TenantUsers.Where(x => x.UserId == user.Id).ToListAsync();
        Assert.Equal(2, memberships.Count);
        Assert.Equal(roleA, memberships.Single(m => m.TenantId == tenantA).TenantRoleId);
        Assert.Equal(roleB, memberships.Single(m => m.TenantId == tenantB).TenantRoleId);
    }

    // ---- TOKEN / OUTBOX -----------------------------------------------------------------

    [Fact]
    public async Task TheOutboxPayloadIsRedactedOnceTheInvitationEmailHasBeenSent()
    {
        await using var factory = new ApiFactory();
        factory.Services.GetRequiredService<FakeEmailSender>().Behavior = FakeEmailBehavior.Success;
        using var client = factory.CreateClient();
        await OnboardAsync(client, "outbox-redact@nexora.test", "outbox-redact");
        var role = await CreateRoleAsync(client, "Recepcao", CustomersReadOnly);
        await CreateInvitationAsync(client, "redactme@nexora.test", role);
        var token = await TokenFromOutboxAsync(factory, "redactme@nexora.test");

        await using (var pump = factory.Services.CreateAsyncScope())
        {
            var processor = pump.ServiceProvider.GetRequiredService<EmailOutboxProcessor>();
            while (await processor.ProcessNextAsync(CancellationToken.None)) { }
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        var message = await db.EmailOutboxMessages
            .SingleAsync(x => x.TemplateKey == InvitationTemplate && x.Recipient == "redactme@nexora.test");
        Assert.Equal(EmailOutboxStatus.Sent, message.Status);
        Assert.Equal("{}", message.Payload);
        Assert.DoesNotContain(token, message.Payload);
        Assert.DoesNotContain("convite/aceitar", message.Payload);
    }

    // ---- AUDITORIA ---------------------------------------------------------------------

    [Fact]
    public async Task AuditDetailsNeverContainTheInviteesPasswordOrItsHash()
    {
        await using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        await OnboardAsync(client, "audit-hash@nexora.test", "audit-hash");
        var role = await CreateRoleAsync(client, "Recepcao", CustomersReadOnly);
        await CreateInvitationAsync(client, "audithash@nexora.test", role);
        (await AcceptAsync(client, await TokenFromOutboxAsync(factory, "audithash@nexora.test"))).EnsureSuccessStatusCode();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        var passwordHash = await db.Users.Where(x => x.NormalizedEmail == "AUDITHASH@NEXORA.TEST")
            .Select(x => x.PasswordHash).SingleAsync();
        var details = await db.AuditLogs.Where(x => x.Details != null).Select(x => x.Details!).ToListAsync();

        Assert.NotEmpty(details);
        Assert.All(details, d =>
        {
            Assert.DoesNotContain(Password, d);
            Assert.DoesNotContain(passwordHash, d);
            Assert.DoesNotContain("passwordHash", d, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("\"password\"", d, StringComparison.OrdinalIgnoreCase);
        });
    }

    // ---- helpers ---------------------------------------------------------------------------

    private sealed record SeededMember(Guid MembershipId, Guid RoleId, Guid UserId, string Email);

    private static async Task<(string Global, string Slug, Guid TenantId)> OnboardAsync(HttpClient client, string email, string slug)
    {
        var global = await RegisterAsync(client, email);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", global);
        var tenant = await client.PostAsJsonAsync("/api/v1/tenants", new { name = slug, slug, timeZoneId = "UTC" });
        tenant.EnsureSuccessStatusCode();
        var tenantId = (await tenant.Content.ReadFromJsonAsync<TenantDto>())!.Id;
        client.DefaultRequestHeaders.Authorization = await ScopedAsync(client, global, slug);
        return (global, slug, tenantId);
    }

    private static async Task<string> RegisterAsync(HttpClient client, string email)
    {
        var response = await client.PostAsJsonAsync("/api/v1/identity/register", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        SetCookie(client, response);
        return (await response.Content.ReadFromJsonAsync<Token>())!.AccessToken;
    }

    /// <summary>Logs an already-registered user in and puts the client on that user's tenant session.</summary>
    private static async Task AuthenticateAsync(HttpClient client, string email, string slug)
    {
        var login = await client.PostAsJsonAsync("/api/v1/identity/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();
        SetCookie(client, login);
        var global = (await login.Content.ReadFromJsonAsync<Token>())!.AccessToken;
        client.DefaultRequestHeaders.Authorization = await ScopedAsync(client, global, slug);
    }

    private static async Task<AuthenticationHeaderValue> ScopedAsync(HttpClient client, string token, string slug)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await client.PostAsJsonAsync($"/api/v1/t/{slug}/session", new { });
        response.EnsureSuccessStatusCode();
        SetCookie(client, response);
        return new AuthenticationHeaderValue("Bearer", (await response.Content.ReadFromJsonAsync<Token>())!.AccessToken);
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

    private static async Task<Guid> CreateInvitationAsync(HttpClient client, string email, Guid roleId)
    {
        var response = await client.PostAsJsonAsync(Invitations, new { email, roleId });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<InvitationView>())!.Id;
    }

    private static Task<HttpResponseMessage> AcceptAsync(HttpClient client, string token) =>
        client.PostAsJsonAsync($"{Invitations}/accept", new { token, password = Password, passwordConfirmation = Password });

    private static async Task<SeededMember> SeedMemberAsync(ApiFactory factory, Guid tenantId, string email, string roleName, string[] permissions)
    {
        using var register = factory.CreateClient();
        (await register.PostAsJsonAsync("/api/v1/identity/register", new { email, password = Password })).EnsureSuccessStatusCode();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        var userId = await db.Users.Where(x => x.Email == email).Select(x => x.Id).SingleAsync();
        var role = new TenantRole(tenantId, roleName, "seeded", false);
        foreach (var key in permissions) role.Permissions.Add(new TenantRolePermission(role.Id, key));
        var membership = new TenantUser(tenantId, userId, role.Id, DateTimeOffset.UtcNow);
        db.TenantRoles.Add(role);
        db.TenantUsers.Add(membership);
        await db.SaveChangesAsync();
        return new SeededMember(membership.Id, role.Id, userId, email);
    }

    private static Task<string> TokenFromOutboxAsync(ApiFactory factory, string recipient) =>
        TokenFromOutboxAsync(factory, recipient, null);

    private static async Task<string> TokenFromOutboxAsync(ApiFactory factory, string recipient, Guid? tenantId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        var payload = await db.EmailOutboxMessages
            .Where(x => x.TemplateKey == InvitationTemplate && x.Recipient == recipient
                && (tenantId == null || x.TenantId == tenantId))
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => x.Payload)
            .FirstAsync();
        var url = JsonSerializer.Deserialize<JsonElement>(payload).GetProperty("AcceptUrl").GetString()!;
        var query = new Uri(url).Query.TrimStart('?');
        var raw = query.Split('&').Select(p => p.Split('=', 2)).First(p => p[0] == "token")[1];
        return Uri.UnescapeDataString(raw);
    }

    private static async Task ShiftExpiryAsync(ApiFactory factory, string recipient, DateTimeOffset expiresAt)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        var invitation = await db.TenantInvitations.SingleAsync(x => x.Email == recipient);
        db.Entry(invitation).Property("ExpiresAt").CurrentValue = expiresAt.ToUniversalTime();
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> UserIdAsync(ApiFactory factory, string normalizedEmail)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<NexoraDbContext>()
            .Users.Where(x => x.NormalizedEmail == normalizedEmail).Select(x => x.Id).SingleAsync();
    }

    private static async Task<List<AuditRow>> AuditRowsAsync(ApiFactory factory, string action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<NexoraDbContext>().AuditLogs
            .Where(x => x.Action == action)
            .Select(x => new AuditRow(x.ActorUserId, x.TenantId, x.Action, x.TargetType, x.TargetId, x.Details))
            .ToListAsync();
    }

    private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private sealed record Token(string AccessToken, DateTimeOffset AccessTokenExpiresAt);
    private sealed record TenantDto(Guid Id);
    private sealed record RoleRef(Guid Id, bool IsSystem);
    private sealed record AuditRow(Guid ActorUserId, Guid? TenantId, string Action, string TargetType, string TargetId, string? Details);
    private sealed record AcceptResult(Guid TenantId, string TenantSlug, string TenantName, bool AccountCreated);
    private sealed record InvitationView(
        Guid Id, string Email, Guid RoleId, string RoleName, string Status, string InvitedByEmail,
        DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, DateTimeOffset? AcceptedAt, DateTimeOffset? CancelledAt);
}
