using Nexora.Application.Tenancy;
using Nexora.Application.Verticals;

namespace Nexora.Api.Verticals;

public static class VerticalSetupEndpoints
{
    public static IEndpointRouteBuilder MapVerticalSetupEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/vertical-setup").WithTags("Vertical setup");
        group.MapGet("/", async (ITenantContext tenant, IVerticalSetupService service, CancellationToken ct) =>
            await service.GetAsync(tenant.TenantId, ct) is { } setup ? Results.Ok(setup) : Results.NotFound())
            .RequireAuthorization(TenantPermissions.ServicesRead);
        group.MapPost("/apply", async (ApplyVerticalSetup request, ITenantContext tenant, IVerticalSetupService service, CancellationToken ct) =>
            Results.Ok(await service.ApplyAsync(tenant.TenantId, request, ct)))
            .RequireAuthorization(TenantPermissions.ServicesCreate);
        return endpoints;
    }
}
