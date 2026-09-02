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
        IPlanCatalogService service,
        CancellationToken ct) =>
        await service.ConfigureOverrideAsync(tenantId, featureId, request.Enabled, request.Limit, ct) is { } result
            ? Results.Ok(result)
            : Results.NotFound();
}
