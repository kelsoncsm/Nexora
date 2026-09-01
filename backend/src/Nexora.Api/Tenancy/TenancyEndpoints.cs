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
        endpoints.MapGet("/api/v1/tenant/members", GetMembersAsync).RequireAuthorization();
        endpoints.MapGet("/api/v1/t/{tenantSlug}/members", GetMembersAsync).RequireAuthorization();
        endpoints.MapPatch("/api/v1/tenant/members/{membershipId:guid}/deactivate", DeactivateAsync).RequireAuthorization();
        return endpoints;
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

    private static async Task<IResult> GetMembersAsync(ITenantContext tenant, ITenancyService service, CancellationToken ct) =>
        tenant.IsAvailable ? Results.Ok(await service.GetMembersAsync(tenant.TenantId, ct)) : Results.Unauthorized();

    private static async Task<IResult> DeactivateAsync(Guid membershipId, ITenantContext tenant, ITenancyService service, CancellationToken ct) =>
        tenant.IsAvailable && await service.DeactivateMembershipAsync(tenant.TenantId, tenant.UserId, membershipId, ct)
            ? Results.NoContent() : Results.NotFound();

    private static Guid UserId(ClaimsPrincipal principal) => Guid.Parse(
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub") ?? throw new InvalidOperationException("Subject claim is missing."));
    private static CookieOptions CookieOptions(HttpContext context) => new()
    { HttpOnly = true, Secure = !context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment(), SameSite = SameSiteMode.Strict,
      Path = "/api/v1", MaxAge = TimeSpan.FromDays(7) };
    private sealed record CreateTenantRequest(string Name, string Slug, string TimeZoneId);
}
