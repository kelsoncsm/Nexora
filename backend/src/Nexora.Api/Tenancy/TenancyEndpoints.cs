using System.Security.Claims;
using Nexora.Application.Identity;
using Nexora.Application.Tenancy;

namespace Nexora.Api.Tenancy;

public static class TenancyEndpoints
{
    private const string RefreshCookie = "nexora_refresh";

    public static IEndpointRouteBuilder MapTenancyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/t/{tenantSlug}", ResolvePublicAsync).AllowAnonymous().WithTags("Tenancy");
        endpoints.MapPost("/api/v1/tenants", CreateAsync).RequireAuthorization().WithTags("Tenancy");
        endpoints.MapPost("/api/v1/t/{tenantSlug}/session", SelectSessionAsync).RequireAuthorization().WithTags("Tenancy");
        endpoints.MapGet("/api/v1/me/tenants", GetMyTenantsAsync).RequireAuthorization().WithTags("Tenancy");
        // Member management has its own CRUD granularity (tenant.members.*, 2026-09-02). Listing
        // members/invitations = read; changing a member's role/status = update; deactivating
        // ("removing") a member or cancelling an invitation = delete; sending/resending an
        // invitation = create. tenant.manage still covers company profile, roles and billing.
        var members = endpoints.MapGroup("/api/v1/tenant/members").WithTags("Tenant Administration");
        members.MapGet("/", GetMembersAsync).RequireAuthorization(TenantPermissions.MembersRead);
        // Lightweight role list for the invite/role pickers — readable by members.read holders who
        // may not have the broader tenant.manage that /tenant/roles requires.
        members.MapGet("/assignable-roles", GetAssignableRolesAsync).RequireAuthorization(TenantPermissions.MembersRead);
        members.MapPatch("/{membershipId:guid}/role", AssignRoleAsync).RequireAuthorization(TenantPermissions.MembersUpdate);
        members.MapPatch("/{membershipId:guid}/deactivate", DeactivateAsync).RequireAuthorization(TenantPermissions.MembersDelete);

        var invitations = members.MapGroup("/invitations");
        invitations.MapGet("/", ListInvitationsAsync).RequireAuthorization(TenantPermissions.MembersRead);
        invitations.MapPost("/", CreateInvitationAsync).RequireAuthorization(TenantPermissions.MembersCreate);
        invitations.MapPost("/{invitationId:guid}/resend", ResendInvitationAsync).RequireAuthorization(TenantPermissions.MembersCreate);
        invitations.MapDelete("/{invitationId:guid}", CancelInvitationAsync).RequireAuthorization(TenantPermissions.MembersDelete);
        // Accepting an invitation happens before the invitee has a tenant session — the token in the
        // body is the only credential. Anonymous + rate-limited (the 256-bit token is the real
        // brute-force defence; the limiter just caps abuse of used/stale tokens).
        invitations.MapPost("/accept", AcceptInvitationAsync).AllowAnonymous().RequireRateLimiting("auth");

        endpoints.MapGet("/api/v1/t/{tenantSlug}/members", GetMembersAsync)
            .RequireAuthorization(TenantPermissions.MembersRead).WithTags("Tenant Administration");

        var admin = endpoints.MapGroup("/api/v1/tenant").RequireAuthorization(TenantPermissions.TenantManage).WithTags("Tenant Administration");
        admin.MapGet("/", GetProfileAsync);
        admin.MapPut("/", UpdateProfileAsync);
        admin.MapGet("/permissions", GetPermissionCatalog);
        admin.MapGet("/roles", GetRolesAsync);
        admin.MapPost("/roles", CreateRoleAsync);
        admin.MapPut("/roles/{roleId:guid}", UpdateRoleAsync);
        admin.MapGet("/roles/{roleId:guid}/permissions", GetRolePermissionsAsync);
        admin.MapPut("/roles/{roleId:guid}/permissions", SetRolePermissionsAsync);
        return endpoints;
    }

    private static async Task<IResult> GetProfileAsync(ITenantContext tenant, ITenancyService service, CancellationToken ct) =>
        tenant.IsAvailable && await service.GetProfileAsync(tenant.TenantId, ct) is { } profile ? Results.Ok(profile) : Results.NotFound();

    private static async Task<IResult> UpdateProfileAsync(UpdateTenantRequest request, ITenantContext tenant, ITenancyService service, CancellationToken ct)
    {
        if (!tenant.IsAvailable) return Results.Unauthorized();
        return Results.Ok(await service.UpdateProfileAsync(tenant.TenantId, request.Name, request.TimeZoneId, ct));
    }

    private static IResult GetPermissionCatalog() => Results.Ok(TenantPermissions.Catalog);

    private static async Task<IResult> GetRolesAsync(ITenantContext tenant, ITenancyService service, CancellationToken ct) =>
        tenant.IsAvailable ? Results.Ok(await service.GetRolesAsync(tenant.TenantId, ct)) : Results.Unauthorized();

    private static async Task<IResult> CreateRoleAsync(RoleRequest request, ITenantContext tenant, ITenancyService service, CancellationToken ct)
    {
        if (!tenant.IsAvailable) return Results.Unauthorized();
        var role = await service.CreateRoleAsync(tenant.TenantId, request.Name, request.Description ?? string.Empty, request.Permissions ?? [], ct);
        return Results.Created($"/api/v1/tenant/roles/{role.Id}", role);
    }

    private static async Task<IResult> UpdateRoleAsync(Guid roleId, RoleRequest request, ITenantContext tenant, ITenancyService service, CancellationToken ct)
    {
        if (!tenant.IsAvailable) return Results.Unauthorized();
        return await service.UpdateRoleAsync(tenant.TenantId, roleId, request.Name, request.Description ?? string.Empty, ct) is { } role
            ? Results.Ok(role) : Results.NotFound();
    }

    private static async Task<IResult> GetRolePermissionsAsync(Guid roleId, ITenantContext tenant, ITenancyService service, CancellationToken ct)
    {
        if (!tenant.IsAvailable) return Results.Unauthorized();
        return await service.GetRoleAsync(tenant.TenantId, roleId, ct) is { } role
            ? Results.Ok(new { role.Id, role.Permissions }) : Results.NotFound();
    }

    private static async Task<IResult> SetRolePermissionsAsync(Guid roleId, RolePermissionsRequest request, ITenantContext tenant, HttpContext http, ITenancyService service, CancellationToken ct)
    {
        if (!tenant.IsAvailable) return Results.Unauthorized();
        return await service.SetRolePermissionsAsync(tenant.TenantId, tenant.UserId, roleId, request.Permissions ?? [], http.TraceIdentifier, ct) is { } role
            ? Results.Ok(role) : Results.NotFound();
    }

    private static async Task<IResult> AssignRoleAsync(Guid membershipId, AssignRoleRequest request, ITenantContext tenant, HttpContext http, ITenancyService service, CancellationToken ct)
    {
        if (!tenant.IsAvailable) return Results.Unauthorized();
        return await service.AssignRoleAsync(tenant.TenantId, tenant.UserId, membershipId, request.RoleId, http.TraceIdentifier, ct) ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> ResolvePublicAsync(string tenantSlug, ITenancyService service, CancellationToken ct) =>
        await service.ResolvePublicAsync(tenantSlug, ct) is { } tenant ? Results.Ok(tenant) : Results.NotFound();

    private static async Task<IResult> CreateAsync(CreateTenantRequest request, ClaimsPrincipal principal, ITenancyService service, CancellationToken ct)
    {
        var userId = UserId(principal); var tenant = await service.CreateAsync(userId, request.Name, request.Slug, request.TimeZoneId, ct);
        return Results.Created($"/api/v1/t/{tenant.Slug}", tenant);
    }

    private static async Task<IResult> SelectSessionAsync(string tenantSlug, ClaimsPrincipal principal, HttpContext context,
        IIdentityService identity, CancellationToken ct)
    {
        if (!context.Request.Cookies.TryGetValue(RefreshCookie, out var refresh)) return Results.Unauthorized();
        var session = await identity.SelectTenantAsync(UserId(principal), tenantSlug, refresh, ct);
        context.Response.Cookies.Append(RefreshCookie, session.RefreshToken, CookieOptions(context));
        return Results.Ok(new { session.AccessToken, session.AccessTokenExpiresAt });
    }

    private static async Task<IResult> GetMyTenantsAsync(ClaimsPrincipal principal, ITenancyService service, CancellationToken ct) =>
        Results.Ok(await service.GetUserTenantsAsync(UserId(principal), ct));

    private static async Task<IResult> GetMembersAsync(ITenantContext tenant, ITenancyService service, CancellationToken ct) =>
        tenant.IsAvailable ? Results.Ok(await service.GetMembersAsync(tenant.TenantId, ct)) : Results.Unauthorized();

    private static async Task<IResult> DeactivateAsync(Guid membershipId, ITenantContext tenant, HttpContext http, ITenancyService service, CancellationToken ct) =>
        tenant.IsAvailable && await service.DeactivateMembershipAsync(tenant.TenantId, tenant.UserId, membershipId, http.TraceIdentifier, ct)
            ? Results.NoContent() : Results.NotFound();

    // ---- Invitations ---------------------------------------------------------------------------

    private static async Task<IResult> GetAssignableRolesAsync(ITenantContext tenant, ITenantInvitationService service, CancellationToken ct) =>
        tenant.IsAvailable ? Results.Ok(await service.GetAssignableRolesAsync(tenant.TenantId, tenant.UserId, ct)) : Results.Unauthorized();

    private static async Task<IResult> ListInvitationsAsync(ITenantContext tenant, ITenantInvitationService service, CancellationToken ct) =>
        tenant.IsAvailable ? Results.Ok(await service.ListAsync(tenant.TenantId, ct)) : Results.Unauthorized();

    private static async Task<IResult> CreateInvitationAsync(
        CreateInvitationRequest request, ITenantContext tenant, HttpContext http, ITenantInvitationService service, CancellationToken ct)
    {
        if (!tenant.IsAvailable) return Results.Unauthorized();
        var view = await service.CreateAsync(tenant.TenantId, tenant.UserId,
            new CreateInvitationInput(request.Email, request.RoleId), http.TraceIdentifier, ct);
        return Results.Created($"/api/v1/tenant/members/invitations/{view.Id}", view);
    }

    private static async Task<IResult> ResendInvitationAsync(
        Guid invitationId, ITenantContext tenant, HttpContext http, ITenantInvitationService service, CancellationToken ct)
    {
        if (!tenant.IsAvailable) return Results.Unauthorized();
        return await service.ResendAsync(tenant.TenantId, tenant.UserId, invitationId, http.TraceIdentifier, ct) is { } view
            ? Results.Ok(view) : Results.NotFound();
    }

    private static async Task<IResult> CancelInvitationAsync(
        Guid invitationId, ITenantContext tenant, HttpContext http, ITenantInvitationService service, CancellationToken ct)
    {
        if (!tenant.IsAvailable) return Results.Unauthorized();
        return await service.CancelAsync(tenant.TenantId, tenant.UserId, invitationId, http.TraceIdentifier, ct)
            ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> AcceptInvitationAsync(
        AcceptInvitationRequest request, HttpContext http, ITenantInvitationService service, CancellationToken ct)
    {
        var result = await service.AcceptAsync(
            new AcceptInvitationInput(request.Token, request.Password, request.PasswordConfirmation), http.TraceIdentifier, ct);
        return Results.Ok(result);
    }

    private static Guid UserId(ClaimsPrincipal principal) => Guid.Parse(
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub") ?? throw new InvalidOperationException("Subject claim is missing."));
    private static CookieOptions CookieOptions(HttpContext context) => new()
    { HttpOnly = true, Secure = !context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment(), SameSite = SameSiteMode.Strict,
      Path = "/api/v1", MaxAge = TimeSpan.FromDays(7) };
    private sealed record CreateTenantRequest(string Name, string Slug, string TimeZoneId);
    private sealed record UpdateTenantRequest(string Name, string TimeZoneId);
    private sealed record RoleRequest(string Name, string? Description, string[]? Permissions);
    private sealed record RolePermissionsRequest(string[]? Permissions);
    private sealed record AssignRoleRequest(Guid RoleId);
    private sealed record CreateInvitationRequest(string Email, Guid RoleId);
    private sealed record AcceptInvitationRequest(string Token, string Password, string? PasswordConfirmation);
}
