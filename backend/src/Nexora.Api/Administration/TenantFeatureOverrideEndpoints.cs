using System.Security.Claims;
using Nexora.Application.Plans;

namespace Nexora.Api.Administration;

internal static class TenantFeatureOverrideEndpoints
{
    public static void MapTenantFeatureOverrideEndpoints(this IEndpointRouteBuilder admin)
    {
        admin.MapGet(
            "/tenants/{tenantId:guid}/feature-overrides",
            (Guid tenantId, IPlanCatalogService service, CancellationToken ct) =>
                service.GetOverridesAsync(tenantId, ct));
        admin.MapPut(
            "/tenants/{tenantId:guid}/feature-overrides/{featureId:guid}",
            ConfigureOverrideAsync);
    }

    private static async Task<IResult> ConfigureOverrideAsync(
        Guid tenantId,
        Guid featureId,
        AdministrationEndpoints.AccessConfiguration request,
        ClaimsPrincipal user,
        HttpContext http,
        IPlanCatalogService service,
        CancellationToken ct) =>
        await service.ConfigureOverrideAsync(UserId(user), tenantId, featureId, request.Enabled, request.Limit, http.TraceIdentifier, ct) is { } result
            ? Results.Ok(result)
            : Results.NotFound();

    private static Guid UserId(ClaimsPrincipal principal) => Guid.Parse(
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub")
        ?? throw new InvalidOperationException("Subject claim is missing."));
}
