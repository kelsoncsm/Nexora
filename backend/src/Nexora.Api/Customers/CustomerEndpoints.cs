using Nexora.Application.Customers;
using Nexora.Application.Tenancy;

namespace Nexora.Api.Customers;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var customers = endpoints.MapGroup("/api/v1/customers").WithTags("Customers");

        customers.MapGet(
                "/",
                (string? search, string? status, string? sort, int? page, int? pageSize,
                    ITenantContext tenant, ICustomerService service, CancellationToken ct) =>
                    service.SearchAsync(tenant.TenantId, search, status, sort ?? "name", page ?? 1, pageSize ?? 20, ct))
            .RequireAuthorization(TenantPermissions.CustomersRead);

        customers.MapGet("/{id:guid}", GetAsync)
            .RequireAuthorization(TenantPermissions.CustomersRead);

        customers.MapPost("/", CreateAsync)
            .RequireAuthorization(TenantPermissions.CustomersCreate);

        customers.MapPut("/{id:guid}", UpdateAsync)
            .RequireAuthorization(TenantPermissions.CustomersUpdate);

        customers.MapDelete("/{id:guid}", DeleteAsync)
            .RequireAuthorization(TenantPermissions.CustomersDelete);

        return endpoints;
    }

    private static async Task<IResult> GetAsync(
        Guid id, ITenantContext tenant, ICustomerService service, CancellationToken ct) =>
        await service.GetAsync(tenant.TenantId, id, ct) is { } customer
            ? Results.Ok(customer)
            : Results.NotFound();

    private static async Task<IResult> CreateAsync(
        CustomerInput input, ITenantContext tenant, ICustomerService service, CancellationToken ct)
    {
        var customer = await service.CreateAsync(tenant.TenantId, input, ct);
        return Results.Created("/api/v1/customers", customer);
    }

    private static async Task<IResult> UpdateAsync(
        Guid id, CustomerInput input, ITenantContext tenant, ICustomerService service, CancellationToken ct) =>
        await service.UpdateAsync(tenant.TenantId, id, input, ct) is { } customer
            ? Results.Ok(customer)
            : Results.NotFound();

    private static async Task<IResult> DeleteAsync(
        Guid id, ITenantContext tenant, ICustomerService service, CancellationToken ct) =>
        await service.DeleteAsync(tenant.TenantId, id, ct)
            ? Results.NoContent()
            : Results.NotFound();
}
