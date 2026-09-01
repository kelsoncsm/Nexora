using Nexora.Domain.Billing;

namespace Nexora.Domain.Onboarding;

public enum OnboardingStatus { InProgress, Completed, Expired }

public sealed class OnboardingDraft
{
    private OnboardingDraft() { }
    public OnboardingDraft(Guid userId, DateTimeOffset now)
    {
        Id = Guid.NewGuid(); UserId = userId; CurrentStep = 1; Status = OnboardingStatus.InProgress;
        CreatedAt = UpdatedAt = now.ToUniversalTime(); ExpiresAt = now.AddDays(30).ToUniversalTime();
    }
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public int CurrentStep { get; private set; }
    public string? CompanyName { get; private set; }
    public string? CompanySlug { get; private set; }
    public Guid? SegmentId { get; private set; }
    public Guid? PlanId { get; private set; }
    public BillingInterval? BillingInterval { get; private set; }
    public string? TimeZoneId { get; private set; }
    public OnboardingStatus Status { get; private set; }
    public Guid? CompletedTenantId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    public void Update(int currentStep, string? companyName, string? companySlug, Guid? segmentId,
        Guid? planId, BillingInterval? billingInterval, string? timeZoneId, DateTimeOffset now)
    {
        EnsureActive(now); CurrentStep = Math.Clamp(currentStep, 1, 5);
        CompanyName = Clean(companyName); CompanySlug = Clean(companySlug)?.ToLowerInvariant(); SegmentId = segmentId;
        PlanId = planId; BillingInterval = billingInterval; TimeZoneId = Clean(timeZoneId);
        UpdatedAt = now.ToUniversalTime(); ExpiresAt = now.AddDays(30).ToUniversalTime();
    }
    public void Complete(Guid tenantId, DateTimeOffset now)
    { EnsureActive(now); Status = OnboardingStatus.Completed; CompletedTenantId = tenantId; UpdatedAt = now.ToUniversalTime(); }
    public void EnsureActive(DateTimeOffset now)
    { if (Status == OnboardingStatus.Completed) throw new InvalidOperationException("Onboarding is already completed."); if (Status == OnboardingStatus.Expired || now > ExpiresAt) { Status = OnboardingStatus.Expired; throw new InvalidOperationException("Onboarding draft has expired."); } }
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
