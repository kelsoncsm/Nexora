using Nexora.Domain.Billing;
using Nexora.Domain.Onboarding;

namespace Nexora.Application.Onboarding;

public sealed record OnboardingDraftView(Guid Id, int CurrentStep, string? CompanyName, string? CompanySlug,
    Guid? SegmentId, Guid? PlanId, BillingInterval? BillingInterval, string? TimeZoneId,
    OnboardingStatus Status, Guid? CompletedTenantId, DateTimeOffset UpdatedAt, DateTimeOffset ExpiresAt);
public sealed record UpdateOnboardingDraft(int CurrentStep, string? CompanyName, string? CompanySlug,
    Guid? SegmentId, Guid? PlanId, BillingInterval? BillingInterval, string? TimeZoneId);
public sealed record OnboardingPlanOption(Guid PlanId, string Code, string Name, BillingInterval BillingInterval,
    decimal Amount, string Currency, IReadOnlyList<OnboardingFeatureLimit> Limits);
public sealed record OnboardingFeatureLimit(string FeatureCode, int? Limit);
public sealed record OnboardingSegment(Guid Id, string Code, string Name);
public sealed record OnboardingCompletion(Guid DraftId, Guid TenantId, string TenantSlug, Guid SubscriptionId,
    DateTimeOffset TrialEndAt);

public interface IOnboardingService
{
    Task<OnboardingDraftView> StartAsync(Guid userId, CancellationToken cancellationToken);
    Task<OnboardingDraftView?> GetAsync(Guid userId, Guid draftId, CancellationToken cancellationToken);
    Task<OnboardingDraftView?> UpdateAsync(Guid userId, Guid draftId, UpdateOnboardingDraft input, CancellationToken cancellationToken);
    Task<IReadOnlyList<OnboardingPlanOption>> GetPlansAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<OnboardingSegment>> GetSegmentsAsync(CancellationToken cancellationToken);
    Task<OnboardingCompletion?> CompleteAsync(Guid userId, Guid draftId, CancellationToken cancellationToken);
}

public sealed class OnboardingValidationException(string message) : Exception(message);
public sealed class OnboardingConflictException(string message) : Exception(message);
