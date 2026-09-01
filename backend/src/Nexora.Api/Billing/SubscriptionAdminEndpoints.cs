using System.Security.Claims;
using Nexora.Application.Billing;

namespace Nexora.Api.Billing;

internal static class SubscriptionAdminEndpoints
{
    public static void MapSubscriptionAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints
            .MapGroup("/api/v1/admin/subscriptions")
            .RequireAuthorization("PlatformAdmin")
            .WithTags("Billing Administration");

        admin.MapGet(
            "/",
            (ISubscriptionService service, CancellationToken ct) => service.GetAllAsync(ct));
        admin.MapGet("/{id:guid}", GetAsync);
        admin.MapGet(
            "/{id:guid}/events",
            (Guid id, ISubscriptionService service, CancellationToken ct) => service.GetEventsAsync(id, ct));
        admin.MapPost("/", CreateTrialAsync);

        admin.MapPost(
            "/{id:guid}/activate",
            (Guid id, ClaimsPrincipal user, HttpContext context, ISubscriptionService service, CancellationToken ct) =>
                Result(service.ActivateAsync(UserId(user), id, context.TraceIdentifier, ct)));
        admin.MapPost(
            "/{id:guid}/change-plan",
            (Guid id, ChangePlanInput input, ClaimsPrincipal user, HttpContext context, ISubscriptionService service, CancellationToken ct) =>
                Result(service.ChangePlanAsync(UserId(user), id, input.PlanId, context.TraceIdentifier, ct)));
        admin.MapPost(
            "/{id:guid}/cancel-at-period-end",
            (Guid id, ClaimsPrincipal user, HttpContext context, ISubscriptionService service, CancellationToken ct) =>
                Result(service.ScheduleCancellationAsync(UserId(user), id, context.TraceIdentifier, ct)));
        admin.MapPost(
            "/{id:guid}/cancel-immediately",
            (Guid id, ClaimsPrincipal user, HttpContext context, ISubscriptionService service, CancellationToken ct) =>
                Result(service.CancelImmediatelyAsync(UserId(user), id, context.TraceIdentifier, ct)));
        admin.MapPost(
            "/{id:guid}/mark-past-due",
            (Guid id, ClaimsPrincipal user, HttpContext context, ISubscriptionService service, CancellationToken ct) =>
                Result(service.MarkPastDueAsync(UserId(user), id, context.TraceIdentifier, ct)));
        admin.MapPost(
            "/{id:guid}/process",
            (Guid id, ClaimsPrincipal user, HttpContext context, ISubscriptionService service, CancellationToken ct) =>
                Result(service.ProcessAsync(UserId(user), id, context.TraceIdentifier, ct)));
    }

    private static async Task<IResult> GetAsync(Guid id, ISubscriptionService service, CancellationToken ct) =>
        await service.GetAsync(id, ct) is { } value ? Results.Ok(value) : Results.NotFound();

    private static async Task<IResult> CreateTrialAsync(
        CreateSubscriptionInput input,
        ClaimsPrincipal user,
        HttpContext context,
        ISubscriptionService service,
        CancellationToken ct)
    {
        var subscription = await service.CreateTrialAsync(UserId(user), input, context.TraceIdentifier, ct);
        return Results.Created("/api/v1/admin/subscriptions", subscription);
    }

    private static async Task<IResult> Result(Task<SubscriptionView?> operation) =>
        await operation is { } value ? Results.Ok(value) : Results.NotFound();

    private static Guid UserId(ClaimsPrincipal principal) => Guid.Parse(
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub")
        ?? throw new InvalidOperationException("Subject claim is missing."));

    private sealed record ChangePlanInput(Guid PlanId);
}
