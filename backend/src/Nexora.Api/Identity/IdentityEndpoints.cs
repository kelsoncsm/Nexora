using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Nexora.Application.Identity;
using Nexora.Application.Plans;
using Nexora.Application.Tenancy;
using Microsoft.AspNetCore.RateLimiting;

namespace Nexora.Api.Identity;

public static class IdentityEndpoints
{
    private const string RefreshCookie = "nexora_refresh";

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/identity").WithTags("Identity");
        group.MapPost("/register", RegisterAsync).AllowAnonymous().RequireRateLimiting("auth");
        group.MapPost("/login", LoginAsync).AllowAnonymous().RequireRateLimiting("auth");
        group.MapPost("/refresh", RefreshAsync).AllowAnonymous().RequireRateLimiting("auth");
        group.MapPost("/logout", LogoutAsync).AllowAnonymous();
        group.MapGet("/me", GetMeAsync).RequireAuthorization(policy => policy.RequireClaim("permission", "identity.profile"));
        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(RegisterCommand command, IIdentityService service,
        HttpContext context, CancellationToken cancellationToken)
    {
        var session = await service.RegisterAsync(command, cancellationToken);
        SetRefreshCookie(context, session.RefreshToken);
        return Results.Created("/api/v1/identity/me", ToResponse(session));
    }

    private static async Task<IResult> LoginAsync(LoginCommand command, IIdentityService service,
        HttpContext context, CancellationToken cancellationToken)
    {
        var session = await service.LoginAsync(command, cancellationToken);
        SetRefreshCookie(context, session.RefreshToken);
        return Results.Ok(ToResponse(session));
    }

    private static async Task<IResult> RefreshAsync(IIdentityService service, HttpContext context,
        CancellationToken cancellationToken)
    {
        if (!context.Request.Cookies.TryGetValue(RefreshCookie, out var token)) return Results.Unauthorized();
        var session = await service.RefreshAsync(token, cancellationToken);
        SetRefreshCookie(context, session.RefreshToken);
        return Results.Ok(ToResponse(session));
    }

    private static async Task<IResult> LogoutAsync(IIdentityService service, HttpContext context,
        CancellationToken cancellationToken)
    {
        if (context.Request.Cookies.TryGetValue(RefreshCookie, out var token))
            await service.LogoutAsync(token, cancellationToken);
        context.Response.Cookies.Delete(RefreshCookie, CookieOptions(context));
        return Results.NoContent();
    }

    private static async Task<IResult> GetMeAsync(ClaimsPrincipal principal, IIdentityService service,
        ITenantContext tenant, IFeatureAccessService features, CancellationToken cancellationToken)
    {
        var subject = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue("sub");
        if (!Guid.TryParse(subject, out var userId) || await service.GetUserAsync(userId, cancellationToken) is not { } user)
            return Results.Unauthorized();

        // Inside a tenant session, surface the effective modules so the SPA can hide menu/routes.
        // The backend endpoint filters remain the authority (ADR-0019).
        if (!tenant.IsAvailable)
            return Results.Ok(new { user.Id, user.Email, user.Permissions });

        var effective = new Dictionary<string, FeatureAccess>(StringComparer.Ordinal);
        foreach (var code in FeatureCodes.All)
            effective[code] = await features.ResolveAsync(tenant.TenantId, code, cancellationToken);
        return Results.Ok(new { user.Id, user.Email, user.Permissions, Features = effective });
    }

    private static object ToResponse(AuthenticatedSession session) => new
    { session.AccessToken, session.AccessTokenExpiresAt };
    private static void SetRefreshCookie(HttpContext context, string token) =>
        context.Response.Cookies.Append(RefreshCookie, token, CookieOptions(context));
    private static CookieOptions CookieOptions(HttpContext context) => new()
    {
        HttpOnly = true,
        Secure = !context.RequestServices.GetRequiredService<IHostEnvironment>().IsDevelopment(),
        SameSite = SameSiteMode.Strict,
        Path = "/api/v1",
        MaxAge = TimeSpan.FromDays(7)
    };
}
