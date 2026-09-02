using System.Data;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Nexora.Application.Onboarding;
using Nexora.Application.Tenancy;
using Nexora.Domain.Administration;
using Nexora.Domain.Billing;
using Nexora.Domain.Onboarding;
using Nexora.Domain.Tenancy;
using Nexora.Infrastructure.Persistence;

namespace Nexora.Infrastructure.Onboarding;

public sealed partial class OnboardingService(NexoraDbContext db, TimeProvider clock) : IOnboardingService
{
    public async Task<OnboardingDraftView> StartAsync(Guid userId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var current = await db.OnboardingDrafts.OrderByDescending(x => x.UpdatedAt)
            .FirstOrDefaultAsync(x => x.UserId == userId && x.Status == OnboardingStatus.InProgress, cancellationToken);
        if (current is not null && current.ExpiresAt >= now) return Map(current);
        if (current is not null) { try { current.EnsureActive(now); } catch (InvalidOperationException) { await db.SaveChangesAsync(cancellationToken); } }
        var draft = new OnboardingDraft(userId, now); db.OnboardingDrafts.Add(draft);
        await SaveAsync(cancellationToken); return Map(draft);
    }

    public async Task<OnboardingDraftView?> GetAsync(Guid userId, Guid draftId, CancellationToken cancellationToken)
    {
        var draft = await db.OnboardingDrafts.SingleOrDefaultAsync(x => x.Id == draftId && x.UserId == userId, cancellationToken);
        if (draft is null) return null;
        if (draft.Status == OnboardingStatus.InProgress && draft.ExpiresAt < clock.GetUtcNow())
        { try { draft.EnsureActive(clock.GetUtcNow()); } catch (InvalidOperationException) { await db.SaveChangesAsync(cancellationToken); } }
        return Map(draft);
    }

    public async Task<OnboardingDraftView?> UpdateAsync(Guid userId, Guid draftId, UpdateOnboardingDraft input, CancellationToken cancellationToken)
    {
        var draft = await db.OnboardingDrafts.SingleOrDefaultAsync(x => x.Id == draftId && x.UserId == userId, cancellationToken);
        if (draft is null) return null;
        try { draft.Update(input.CurrentStep, input.CompanyName, input.CompanySlug, input.SegmentId, input.PlanId, input.BillingInterval, input.TimeZoneId, clock.GetUtcNow()); }
        catch (InvalidOperationException exception) { throw new OnboardingValidationException(exception.Message); }
        await SaveAsync(cancellationToken); return Map(draft);
    }

    public async Task<IReadOnlyList<OnboardingPlanOption>> GetPlansAsync(CancellationToken cancellationToken)
    {
        var plans = await db.Plans.AsNoTracking().Include(x => x.Features).ThenInclude(x => x.Feature)
            .Where(x => x.IsActive && x.IsPublic && x.IsTrialEligible).ToArrayAsync(cancellationToken);
        var planIds = plans.Select(x => x.Id).ToArray();
        var prices = await db.PlanPrices.AsNoTracking().Where(x => x.IsActive && planIds.Contains(x.PlanId))
            .OrderBy(x => x.BillingInterval).ToArrayAsync(cancellationToken);
        return (from plan in plans
            from price in prices.Where(x => x.PlanId == plan.Id)
            orderby plan.Name, price.BillingInterval
            select new OnboardingPlanOption(plan.Id, plan.Code, plan.Name, price.BillingInterval, price.Amount,
                price.Currency, plan.Features.Where(x => x.Enabled)
                    .Select(x => new OnboardingFeatureLimit(x.Feature.Code, x.Limit)).ToArray())).ToArray();
    }

    public async Task<IReadOnlyList<OnboardingSegment>> GetSegmentsAsync(CancellationToken cancellationToken) =>
        await db.BusinessSegments.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name)
            .Select(x => new OnboardingSegment(x.Id, x.Code, x.Name)).ToArrayAsync(cancellationToken);

    public async Task<OnboardingCompletion?> CompleteAsync(Guid userId, Guid draftId, CancellationToken cancellationToken)
    {
        var usesPostgreSql = db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL";
        await using var transaction = usesPostgreSql
            ? await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken) : null;
        try
        {
            var draft = await LoadForCompletionAsync(userId, draftId, cancellationToken);
            if (draft is null) return null;
            if (draft.Status == OnboardingStatus.Completed) return await ExistingCompletionAsync(draft, cancellationToken);
            ValidateRequired(draft);
            var now = clock.GetUtcNow();
            try { draft.EnsureActive(now); } catch (InvalidOperationException exception) { throw new OnboardingValidationException(exception.Message); }
            var segmentValid = await db.BusinessSegments.AnyAsync(x => x.Id == draft.SegmentId && x.IsActive, cancellationToken);
            if (!segmentValid) throw new OnboardingValidationException("Segment is unavailable.");
            var plan = await db.Plans.Include(x => x.Features).ThenInclude(x => x.Feature)
                .SingleOrDefaultAsync(x => x.Id == draft.PlanId && x.IsActive && x.IsPublic && x.IsTrialEligible, cancellationToken)
                ?? throw new OnboardingValidationException("Plan is not available for onboarding.");
            var priceValid = await db.PlanPrices.AnyAsync(x => x.PlanId == plan.Id && x.BillingInterval == draft.BillingInterval && x.IsActive, cancellationToken);
            if (!priceValid) throw new OnboardingValidationException("Plan price is unavailable for the selected interval.");
            ValidateTimeZone(draft.TimeZoneId!); ValidateCompany(draft.CompanyName!, draft.CompanySlug!);
            if (await db.Tenants.AnyAsync(x => x.Slug == draft.CompanySlug, cancellationToken)) throw new OnboardingConflictException("Tenant slug is unavailable.");

            var tenant = new Tenant(draft.CompanyName!, draft.CompanySlug!, draft.TimeZoneId!, now, draft.SegmentId);
            tenant.AddInitialAdministrator(userId, TenantPermissions.All, now);
            var subscription = new Subscription(tenant.Id, plan.Id, draft.BillingInterval!.Value, now, SubscriptionTrialPolicy.Duration);
            subscription.Events.Add(new SubscriptionEvent(subscription.Id, userId, "subscription.trial_started", $"planId={plan.Id};interval={draft.BillingInterval}", now));
            db.Tenants.Add(tenant); db.Subscriptions.Add(subscription); draft.Complete(tenant.Id, now);
            await db.SaveChangesAsync(cancellationToken);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return new(draft.Id, tenant.Id, tenant.Slug, subscription.Id, subscription.TrialEndAt);
        }
        catch (DbUpdateException exception)
        {
            if (transaction is not null) await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            var completed = await db.OnboardingDrafts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == draftId && x.UserId == userId && x.Status == OnboardingStatus.Completed, cancellationToken);
            if (completed is not null) return await ExistingCompletionAsync(completed, cancellationToken);
            throw new OnboardingConflictException($"Onboarding completion conflicted with persisted data: {exception.GetType().Name}.");
        }
    }

    private Task<OnboardingDraft?> LoadForCompletionAsync(Guid userId, Guid draftId, CancellationToken cancellationToken) =>
        db.Database.ProviderName == "Npgsql.EntityFrameworkCore.PostgreSQL"
            ? db.OnboardingDrafts.FromSqlInterpolated($"SELECT * FROM onboarding.onboarding_drafts WHERE \"Id\" = {draftId} AND \"UserId\" = {userId} FOR UPDATE").SingleOrDefaultAsync(cancellationToken)
            : db.OnboardingDrafts.SingleOrDefaultAsync(x => x.Id == draftId && x.UserId == userId, cancellationToken);
    private async Task<OnboardingCompletion> ExistingCompletionAsync(OnboardingDraft draft, CancellationToken cancellationToken)
    {
        var tenant = await db.Tenants.AsNoTracking().SingleAsync(x => x.Id == draft.CompletedTenantId, cancellationToken);
        var subscription = await db.Subscriptions.AsNoTracking().SingleAsync(x => x.TenantId == tenant.Id && x.Status == SubscriptionStatus.Trialing, cancellationToken);
        return new(draft.Id, tenant.Id, tenant.Slug, subscription.Id, subscription.TrialEndAt);
    }
    private static void ValidateRequired(OnboardingDraft draft)
    { if (draft.CompanyName is null || draft.CompanySlug is null || draft.SegmentId is null || draft.PlanId is null || draft.BillingInterval is null || draft.TimeZoneId is null) throw new OnboardingValidationException("Onboarding draft is incomplete."); }
    private static void ValidateCompany(string name, string slug)
    { if (name.Length is < 2 or > 200 || slug.Length is < 3 or > 100 || !SlugPattern().IsMatch(slug)) throw new OnboardingValidationException("Company name or slug is invalid."); }
    private static void ValidateTimeZone(string timeZoneId)
    { if (!TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out var zone) || !zone.HasIanaId) throw new OnboardingValidationException("TimeZoneId must be a valid IANA identifier."); }
    private async Task SaveAsync(CancellationToken cancellationToken)
    { try { await db.SaveChangesAsync(cancellationToken); } catch (DbUpdateException) { throw new OnboardingConflictException("An active onboarding draft already exists."); } }
    private static OnboardingDraftView Map(OnboardingDraft x) => new(x.Id, x.CurrentStep, x.CompanyName, x.CompanySlug, x.SegmentId, x.PlanId, x.BillingInterval, x.TimeZoneId, x.Status, x.CompletedTenantId, x.UpdatedAt, x.ExpiresAt);
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)] private static partial Regex SlugPattern();
}
