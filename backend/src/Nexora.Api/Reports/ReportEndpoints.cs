using Nexora.Application.Reports;
using Nexora.Application.Tenancy;

namespace Nexora.Api.Reports;

public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/v1/reports/overview", (DateTimeOffset from, DateTimeOffset to, IReportService service, CancellationToken ct) =>
                service.GetTenantReportAsync(from, to, ct))
            .RequireAuthorization(TenantPermissions.ReportsRead).WithTags("Reports");
        endpoints.MapGet("/api/v1/admin/reports/overview", (DateTimeOffset from, DateTimeOffset to, IReportService service, CancellationToken ct) =>
                service.GetPlatformReportAsync(from, to, ct))
            .RequireAuthorization("PlatformAdmin").WithTags("Platform Reports");
        return endpoints;
    }
}
