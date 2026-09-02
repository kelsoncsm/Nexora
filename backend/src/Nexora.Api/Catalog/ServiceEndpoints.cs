using Nexora.Application.Catalog;
using Nexora.Application.Tenancy;

namespace Nexora.Api.Catalog;

internal static class ServiceEndpoints
{
    public static void MapServiceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var services = endpoints.MapGroup("/api/v1/services").WithTags("Services");

        services.MapGet(
                "/",
                (ITenantContext tenant, ICatalogService service, CancellationToken ct) =>
                    service.GetServicesAsync(tenant.TenantId, ct))
            .RequireAuthorization(TenantPermissions.ServicesRead);

        services.MapPost("/", CreateServiceAsync)
            .RequireAuthorization(TenantPermissions.ServicesCreate);

        services.MapPut("/{id:guid}", UpdateServiceAsync)
            .RequireAuthorization(TenantPermissions.ServicesUpdate);

        services.MapDelete("/{id:guid}", DeleteServiceAsync)
            .RequireAuthorization(TenantPermissions.ServicesDelete);
    }

    private static async Task<IResult> CreateServiceAsync(
        ServiceInput input, ITenantContext tenant, ICatalogService service, CancellationToken ct)
    {
        var created = await service.CreateServiceAsync(tenant.TenantId, input, ct);
        return Results.Created("/api/v1/services", created);
    }

    private static async Task<IResult> UpdateServiceAsync(
        Guid id, ServiceInput input, ITenantContext tenant, ICatalogService service, CancellationToken ct) =>
        await service.UpdateServiceAsync(tenant.TenantId, id, input, ct) is { } updated
            ? Results.Ok(updated)
            : Results.NotFound();

    private static async Task<IResult> DeleteServiceAsync(
        Guid id, ITenantContext tenant, ICatalogService service, CancellationToken ct) =>
        await service.DeleteServiceAsync(tenant.TenantId, id, ct)
            ? Results.NoContent()
            : Results.NotFound();
}
