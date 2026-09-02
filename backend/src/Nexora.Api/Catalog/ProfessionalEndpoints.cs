using Nexora.Application.Catalog;
using Nexora.Application.Tenancy;

namespace Nexora.Api.Catalog;

internal static class ProfessionalEndpoints
{
    public static void MapProfessionalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var professionals = endpoints.MapGroup("/api/v1/professionals").WithTags("Professionals");

        professionals.MapGet(
                "/",
                (ITenantContext tenant, ICatalogService service, CancellationToken ct) =>
                    service.GetProfessionalsAsync(tenant.TenantId, ct))
            .RequireAuthorization(TenantPermissions.ProfessionalsRead);

        professionals.MapGet("/{id:guid}", GetProfessionalAsync)
            .RequireAuthorization(TenantPermissions.ProfessionalsRead);

        professionals.MapPost("/", CreateProfessionalAsync)
            .RequireAuthorization(TenantPermissions.ProfessionalsCreate);

        professionals.MapPut("/{id:guid}", UpdateProfessionalAsync)
            .RequireAuthorization(TenantPermissions.ProfessionalsUpdate);

        professionals.MapDelete("/{id:guid}", DeleteProfessionalAsync)
            .RequireAuthorization(TenantPermissions.ProfessionalsDelete);

        professionals.MapPut("/{id:guid}/services/{serviceId:guid}", LinkServiceAsync)
            .RequireAuthorization(TenantPermissions.ProfessionalsUpdate);

        professionals.MapDelete("/{id:guid}/services/{serviceId:guid}", UnlinkServiceAsync)
            .RequireAuthorization(TenantPermissions.ProfessionalsUpdate);
    }

    private static async Task<IResult> GetProfessionalAsync(
        Guid id, ITenantContext tenant, ICatalogService service, CancellationToken ct) =>
        await service.GetProfessionalAsync(tenant.TenantId, id, ct) is { } professional
            ? Results.Ok(professional)
            : Results.NotFound();

    private static async Task<IResult> CreateProfessionalAsync(
        ProfessionalInput input, ITenantContext tenant, ICatalogService service, CancellationToken ct)
    {
        var professional = await service.CreateProfessionalAsync(tenant.TenantId, input, ct);
        return Results.Created("/api/v1/professionals", professional);
    }

    private static async Task<IResult> UpdateProfessionalAsync(
        Guid id, ProfessionalInput input, ITenantContext tenant, ICatalogService service, CancellationToken ct) =>
        await service.UpdateProfessionalAsync(tenant.TenantId, id, input, ct) is { } professional
            ? Results.Ok(professional)
            : Results.NotFound();

    private static async Task<IResult> DeleteProfessionalAsync(
        Guid id, ITenantContext tenant, ICatalogService service, CancellationToken ct) =>
        await service.DeleteProfessionalAsync(tenant.TenantId, id, ct)
            ? Results.NoContent()
            : Results.NotFound();

    private static async Task<IResult> LinkServiceAsync(
        Guid id, Guid serviceId, ITenantContext tenant, ICatalogService service, CancellationToken ct) =>
        await service.LinkAsync(tenant.TenantId, id, serviceId, ct)
            ? Results.NoContent()
            : Results.NotFound();

    private static async Task<IResult> UnlinkServiceAsync(
        Guid id, Guid serviceId, ITenantContext tenant, ICatalogService service, CancellationToken ct) =>
        await service.UnlinkAsync(tenant.TenantId, id, serviceId, ct)
            ? Results.NoContent()
            : Results.NotFound();
}
