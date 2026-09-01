using System.Security.Claims;
using Nexora.Application.Onboarding;

namespace Nexora.Api.Onboarding;

public static class OnboardingEndpoints
{
    public static IEndpointRouteBuilder MapOnboardingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var onboarding = endpoints.MapGroup("/api/v1/onboarding")
            .RequireAuthorization()
            .RequireRateLimiting("onboarding")
            .WithTags("Self-service onboarding");

        onboarding.MapPost("/drafts", async (ClaimsPrincipal user, IOnboardingService service, CancellationToken ct) =>
        {
            var draft = await service.StartAsync(UserId(user), ct);
            return Results.Ok(draft);
        });
        onboarding.MapGet("/drafts/{id:guid}", async (Guid id, ClaimsPrincipal user, IOnboardingService service, CancellationToken ct) =>
            await service.GetAsync(UserId(user), id, ct) is { } draft ? Results.Ok(draft) : Results.NotFound());
        onboarding.MapPut("/drafts/{id:guid}", async (Guid id, UpdateOnboardingDraft request, ClaimsPrincipal user, IOnboardingService service, CancellationToken ct) =>
            await service.UpdateAsync(UserId(user), id, request, ct) is { } draft ? Results.Ok(draft) : Results.NotFound());
        onboarding.MapPost("/drafts/{id:guid}/complete", async (Guid id, ClaimsPrincipal user, IOnboardingService service, CancellationToken ct) =>
            await service.CompleteAsync(UserId(user), id, ct) is { } completion ? Results.Ok(completion) : Results.NotFound());
        onboarding.MapGet("/segments", (IOnboardingService service, CancellationToken ct) => service.GetSegmentsAsync(ct));
        onboarding.MapGet("/plans", (IOnboardingService service, CancellationToken ct) => service.GetPlansAsync(ct));

        return endpoints;
    }

    private static Guid UserId(ClaimsPrincipal principal) => Guid.Parse(
        principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub")
        ?? throw new InvalidOperationException("Subject claim is missing."));
}
