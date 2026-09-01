namespace Nexora.Domain.Billing;

public enum SubscriptionStatus { Trialing, Active, PastDue, Canceled, Expired }
public enum BillingInterval { Monthly, Yearly }

public static class SubscriptionTrialPolicy
{
    public static readonly TimeSpan Duration = TimeSpan.FromDays(14);
}

public readonly record struct BillingPeriod(DateTimeOffset Start, DateTimeOffset End);
public static class BillingPeriodPolicy
{
    public static BillingPeriod Next(Subscription subscription)
    {
        var start = subscription.Status == SubscriptionStatus.Trialing
            ? subscription.TrialEndAt : subscription.CurrentPeriodEnd;
        return new(start, AddInterval(start, subscription.BillingInterval));
    }
    public static DateTimeOffset AddInterval(DateTimeOffset value, BillingInterval interval) =>
        (interval == BillingInterval.Monthly ? value.AddMonths(1) : value.AddYears(1)).ToUniversalTime();
}

public sealed class Subscription
{
    private Subscription() { }
    public Subscription(Guid tenantId,Guid planId,BillingInterval interval,DateTimeOffset now,TimeSpan trialDuration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(trialDuration,TimeSpan.Zero);
        Id=Guid.NewGuid();TenantId=tenantId;PlanId=planId;BillingInterval=interval;Status=SubscriptionStatus.Trialing;
        TrialStartAt=now.ToUniversalTime();TrialEndAt=now.Add(trialDuration).ToUniversalTime();CurrentPeriodStart=TrialStartAt;CurrentPeriodEnd=TrialEndAt;CreatedAt=UpdatedAt=TrialStartAt;
    }
    public Guid Id{get;private set;}public Guid TenantId{get;private set;}public Guid PlanId{get;private set;}
    public SubscriptionStatus Status{get;private set;}public BillingInterval BillingInterval{get;private set;}
    public DateTimeOffset TrialStartAt{get;private set;}public DateTimeOffset TrialEndAt{get;private set;}
    public DateTimeOffset CurrentPeriodStart{get;private set;}public DateTimeOffset CurrentPeriodEnd{get;private set;}
    public bool CancelAtPeriodEnd{get;private set;}public DateTimeOffset? CanceledAt{get;private set;}public DateTimeOffset? PastDueSince{get;private set;}
    public DateTimeOffset CreatedAt{get;private set;}public DateTimeOffset UpdatedAt{get;private set;}
    public ICollection<SubscriptionEvent> Events{get;}=[];
    public void Activate(DateTimeOffset now){ActivateForPeriod(now, BillingPeriodPolicy.AddInterval(now,BillingInterval), now);}
    public void ActivateForPeriod(DateTimeOffset start,DateTimeOffset end,DateTimeOffset now){Ensure(Status is SubscriptionStatus.Trialing or SubscriptionStatus.PastDue,"Only trialing or past-due subscriptions can be activated.");Ensure(end>start,"Paid coverage period is invalid.");Status=SubscriptionStatus.Active;PastDueSince=null;CancelAtPeriodEnd=false;CanceledAt=null;CurrentPeriodStart=start.ToUniversalTime();CurrentPeriodEnd=end.ToUniversalTime();Touch(now);}
    public void AdvancePaidPeriod(DateTimeOffset start,DateTimeOffset end,DateTimeOffset now){Ensure(Status==SubscriptionStatus.Active&&start.ToUniversalTime()==CurrentPeriodEnd,"Paid coverage must continue the active period.");Ensure(end>start,"Paid coverage period is invalid.");CurrentPeriodStart=start.ToUniversalTime();CurrentPeriodEnd=end.ToUniversalTime();Touch(now);}
    public bool ApplyPaidCoverage(DateTimeOffset start,DateTimeOffset end,DateTimeOffset now)
    {
        var utc=now.ToUniversalTime();var coverageStart=start.ToUniversalTime();var coverageEnd=end.ToUniversalTime();
        if(Status==SubscriptionStatus.Trialing){if(utc<TrialEndAt)return false;Ensure(coverageStart==TrialEndAt,"Paid coverage must start when the trial ends.");ActivateForPeriod(coverageStart,coverageEnd,utc);return true;}
        if(Status==SubscriptionStatus.PastDue){ActivateForPeriod(coverageStart,coverageEnd,utc);return true;}
        if(Status==SubscriptionStatus.Active&&coverageStart==CurrentPeriodEnd){AdvancePaidPeriod(coverageStart,coverageEnd,utc);return true;}
        return false;
    }
    public void ChangePlan(Guid planId,DateTimeOffset now){Ensure(IsLive,"Plan can only be changed on a live subscription.");PlanId=planId;Touch(now);}
    public void ScheduleCancellation(DateTimeOffset now){Ensure(Status==SubscriptionStatus.Active,"Only active subscriptions can be canceled at period end.");CancelAtPeriodEnd=true;CanceledAt=now.ToUniversalTime();Touch(now);}
    public void CancelImmediately(DateTimeOffset now){Ensure(IsLive,"Only a live subscription can be canceled.");Status=SubscriptionStatus.Canceled;CancelAtPeriodEnd=false;CanceledAt=now.ToUniversalTime();PastDueSince=null;Touch(now);}
    public void MarkPastDue(DateTimeOffset now){Ensure(Status==SubscriptionStatus.Active,"Only an active subscription can become past due.");Status=SubscriptionStatus.PastDue;PastDueSince=now.ToUniversalTime();CancelAtPeriodEnd=false;Touch(now);}
    public bool Evaluate(DateTimeOffset now,TimeSpan gracePeriod)
    {
        var utc=now.ToUniversalTime();var next=Status;
        if(Status==SubscriptionStatus.Trialing&&utc>=TrialEndAt)next=SubscriptionStatus.Expired;
        else if(Status==SubscriptionStatus.PastDue&&PastDueSince.HasValue&&utc>=PastDueSince.Value.Add(gracePeriod))next=SubscriptionStatus.Expired;
        else if(Status==SubscriptionStatus.Active&&utc>=CurrentPeriodEnd)next=CancelAtPeriodEnd?SubscriptionStatus.Canceled:SubscriptionStatus.Expired;
        if(next==Status)return false;Status=next;if(next==SubscriptionStatus.Canceled)CanceledAt??=utc;Touch(utc);return true;
    }
    public bool GrantsEntitlement(DateTimeOffset now,TimeSpan gracePeriod){Evaluate(now,gracePeriod);return Status is SubscriptionStatus.Trialing or SubscriptionStatus.Active or SubscriptionStatus.PastDue;}
    public bool IsLive=>Status is SubscriptionStatus.Trialing or SubscriptionStatus.Active or SubscriptionStatus.PastDue;
    private void Touch(DateTimeOffset now)=>UpdatedAt=now.ToUniversalTime();
    private static void Ensure(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
}

public sealed class SubscriptionEvent
{
    private SubscriptionEvent() { }
    public SubscriptionEvent(Guid subscriptionId,Guid? actorUserId,string eventType,string? details,DateTimeOffset occurredAt)
    {Id=Guid.NewGuid();SubscriptionId=subscriptionId;ActorUserId=actorUserId;EventType=eventType;Details=details;OccurredAt=occurredAt.ToUniversalTime();}
    public Guid Id{get;private set;}public Guid SubscriptionId{get;private set;}public Subscription Subscription{get;private set;}=null!;public Guid? ActorUserId{get;private set;}public string EventType{get;private set;}=string.Empty;public string? Details{get;private set;}public DateTimeOffset OccurredAt{get;private set;}
}
