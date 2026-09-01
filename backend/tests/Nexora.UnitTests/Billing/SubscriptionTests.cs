using Nexora.Domain.Billing;

namespace Nexora.UnitTests.Billing;

public sealed class SubscriptionTests
{
    private static readonly DateTimeOffset Now=new(2026,9,1,12,0,0,TimeSpan.Zero);
    [Fact]public void TrialExpiresAndNoLongerGrantsEntitlement(){var value=Create();Assert.Equal(SubscriptionStatus.Trialing,value.Status);Assert.True(value.GrantsEntitlement(Now.AddDays(13),TimeSpan.FromDays(7)));Assert.False(value.GrantsEntitlement(Now.AddDays(14),TimeSpan.FromDays(7)));Assert.Equal(SubscriptionStatus.Expired,value.Status);}
    [Fact]public void ActivationCreatesPeriodAndPlanChangeIsImmediate(){var value=Create();var plan=Guid.NewGuid();value.Activate(Now.AddDays(1));value.ChangePlan(plan,Now.AddDays(2));Assert.Equal(SubscriptionStatus.Active,value.Status);Assert.Equal(plan,value.PlanId);Assert.Equal(Now.AddDays(1).AddMonths(1),value.CurrentPeriodEnd);}
    [Fact]public void ScheduledCancellationKeepsEntitlementUntilPeriodEnd(){var value=Create();value.Activate(Now);value.ScheduleCancellation(Now.AddDays(1));Assert.True(value.GrantsEntitlement(Now.AddDays(20),TimeSpan.FromDays(7)));Assert.False(value.GrantsEntitlement(Now.AddMonths(1),TimeSpan.FromDays(7)));Assert.Equal(SubscriptionStatus.Canceled,value.Status);}
    [Fact]public void PastDueHasSevenDayGraceThenExpires(){var value=Create();value.Activate(Now);value.MarkPastDue(Now.AddDays(1));Assert.True(value.GrantsEntitlement(Now.AddDays(7),TimeSpan.FromDays(7)));Assert.False(value.GrantsEntitlement(Now.AddDays(8),TimeSpan.FromDays(7)));Assert.Equal(SubscriptionStatus.Expired,value.Status);}
    [Fact]public void ImmediateCancellationAndInvalidTransitionsAreEnforced(){var value=Create();value.CancelImmediately(Now);Assert.Equal(SubscriptionStatus.Canceled,value.Status);Assert.False(value.GrantsEntitlement(Now,TimeSpan.FromDays(7)));Assert.Throws<InvalidOperationException>(()=>value.MarkPastDue(Now));Assert.Throws<InvalidOperationException>(()=>value.Activate(Now));}
    [Fact]public void ActivePeriodWithoutRenewalExpires(){var value=Create();value.Activate(Now);Assert.False(value.GrantsEntitlement(Now.AddMonths(1),TimeSpan.FromDays(7)));Assert.Equal(SubscriptionStatus.Expired,value.Status);}
    private static Subscription Create()=>new(Guid.NewGuid(),Guid.NewGuid(),BillingInterval.Monthly,Now,TimeSpan.FromDays(14));
}
