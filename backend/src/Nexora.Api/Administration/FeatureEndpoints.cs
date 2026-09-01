using Nexora.Application.Plans;

namespace Nexora.Api.Administration;

internal static class FeatureEndpoints
{
    public static void MapFeatureEndpoints(this IEndpointRouteBuilder admin)
    {
        admin.MapGet(
            "/features",
            (IPlanCatalogService service, CancellationToken ct) => service.GetFeaturesAsync(ct));
        admin.MapPost("/features", CreateFeatureAsync);
        admin.MapPut("/features/{id:guid}", UpdateFeatureAsync);
    }

    private static async Task<IResult> CreateFeatureAsync(
        AdministrationEndpoints.CatalogCreate request,
        IPlanCatalogService service,
        CancellationToken ct)
    {
        var feature = await service.CreateFeatureAsync(request.Code, request.Name, ct);
        return Results.Created("/api/v1/admin/features", feature);
    }

    private static async Task<IResult> UpdateFeatureAsync(
        Guid id,
        AdministrationEndpoints.CatalogUpdate request,
        IPlanCatalogService service,
        CancellationToken ct) =>
        await service.UpdateFeatureAsync(id, request.Name, request.IsActive, ct) is { } feature
            ? Results.Ok(feature)
            : Results.NotFound();
}
