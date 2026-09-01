using Nexora.Domain.Onboarding;

namespace Nexora.UnitTests.Architecture;

public sealed class OnboardingDraftTests
{
    [Fact]
    public void DraftExpiresAfterThirtyDaysOfInactivity()
    {
        var now=new DateTimeOffset(2026,9,1,12,0,0,TimeSpan.Zero);var draft=new OnboardingDraft(Guid.NewGuid(),now);
        var exception=Assert.Throws<InvalidOperationException>(()=>draft.EnsureActive(now.AddDays(30).AddTicks(1)));
        Assert.Equal(OnboardingStatus.Expired,draft.Status);Assert.Contains("expired",exception.Message,StringComparison.OrdinalIgnoreCase);
    }
}
