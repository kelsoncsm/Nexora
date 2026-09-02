using System.Security.Claims;
using Nexora.Application.Billing;

namespace Nexora.Api.Billing;

internal static class BillingAdminEndpoints
{
    public static void MapBillingAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var payments = endpoints
            .MapGroup("/api/v1/admin/billing")
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Billing Administration");

        payments.MapGet(
            "/prices",
            (IBillingPaymentService service, CancellationToken ct) => service.GetPricesAsync(ct));
        payments.MapPost("/prices", SetPriceAsync);
        payments.MapGet(
            "/invoices",
            (IBillingPaymentService service, CancellationToken ct) => service.GetInvoicesAsync(ct));
        payments.MapGet(
            "/payments",
            (IBillingPaymentService service, CancellationToken ct) => service.GetPaymentsAsync(ct));
    }

    private static async Task<IResult> SetPriceAsync(
        CreatePlanPriceInput input,
        ClaimsPrincipal user,
        HttpContext context,
        IBillingPaymentService service,
        CancellationToken ct)
    {
        var price = await service.SetPriceAsync(UserId(user), input, context.TraceIdentifier, ct);
        return Results.Created("/api/v1/admin/billing/prices", price);
    }

    private static Guid UserId(ClaimsPrincipal principal) => Guid.Parse(
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub")
        ?? throw new InvalidOperationException("Subject claim is missing."));
}
