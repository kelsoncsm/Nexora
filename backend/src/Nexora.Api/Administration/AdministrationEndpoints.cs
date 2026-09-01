namespace Nexora.Api.Administration;

public static class AdministrationEndpoints
{
    public static IEndpointRouteBuilder MapAdministrationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints
            .MapGroup("/api/v1/admin")
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Platform Administration");

        admin.MapDashboardEndpoints();
        admin.MapTenantAdminEndpoints();
        admin.MapUserAdminEndpoints();
        admin.MapSegmentEndpoints();
        admin.MapAuditEndpoints();
        admin.MapFeatureEndpoints();
        admin.MapPlanEndpoints();
        admin.MapTenantFeatureOverrideEndpoints();

        return endpoints;
    }

    // Request payloads for the platform administration endpoints. Kept next to the module that
    // owns them (rule 9); each is used by a single endpoint group below.
    internal sealed record StatusRequest(bool IsActive);
    internal sealed record SegmentCreateRequest(string Code, string Name);
    internal sealed record SegmentUpdateRequest(string Name, bool IsActive);
    internal sealed record CatalogCreate(string Code, string Name);
    internal sealed record CatalogUpdate(string Name, bool IsActive);
    internal sealed record PlanUpdate(string Name, bool IsActive, bool IsPublic, bool IsTrialEligible);
    internal sealed record AccessConfiguration(bool Enabled, int? Limit);
}
