using System.Security.Claims;
using Nexora.Application.Identity;
using Nexora.Application.Tenancy;

namespace Nexora.Api.Tenancy;

public sealed class TenantContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenancyService tenancy, ITenantContextInitializer initializer)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            context.User.FindFirstValue("tenant_id") is { } tenantClaim)
        {
            var subject = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? context.User.FindFirstValue("sub");
            if (!Guid.TryParse(subject, out var userId) || !Guid.TryParse(tenantClaim, out var tenantId) ||
                !await tenancy.ValidateMembershipAsync(tenantId, userId, context.RequestAborted))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized; return;
            }

            if (context.Request.RouteValues.TryGetValue("tenantSlug", out var slugValue))
            {
                PublicTenant? routeTenant;
                try { routeTenant = await tenancy.ResolvePublicAsync(slugValue as string ?? string.Empty, context.RequestAborted); }
                catch (TenantValidationException) { routeTenant = null; }
                if (routeTenant?.Id != tenantId) { context.Response.StatusCode = StatusCodes.Status404NotFound; return; }
            }
            initializer.Initialize(tenantId, userId);
            if(context.User.Identity is ClaimsIdentity identity)
            {
                // Swap the token's permission claims for the tenant-scoped grants, but keep the
                // global identity permissions (e.g. identity.profile) so the user's own identity
                // endpoints stay reachable inside a tenant session.
                foreach(var claim in identity.FindAll("permission").ToArray())
                    if(!GlobalPermissions.All.Contains(claim.Value,StringComparer.Ordinal))
                        identity.RemoveClaim(claim);
                var existing=identity.FindAll("permission").Select(x=>x.Value).ToHashSet(StringComparer.Ordinal);
                foreach(var permission in await tenancy.GetPermissionsAsync(tenantId,userId,context.RequestAborted))
                    if(existing.Add(permission))identity.AddClaim(new Claim("permission",permission));
            }
        }
        await next(context);
    }
}

public static class TenantContextMiddlewareExtensions
{
    public static IApplicationBuilder UseTenantContext(this IApplicationBuilder app) => app.UseMiddleware<TenantContextMiddleware>();
}
