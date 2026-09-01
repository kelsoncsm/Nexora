namespace Nexora.Api.Catalog;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapProfessionalEndpoints();
        endpoints.MapServiceEndpoints();
        return endpoints;
    }
}
