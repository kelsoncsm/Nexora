using Microsoft.EntityFrameworkCore;
using Nexora.Application.Billing;
using Nexora.Application.Plans;
using Nexora.Domain.Administration;
using Nexora.Domain.Billing;
using Nexora.Infrastructure.Persistence;

namespace Nexora.Infrastructure.Billing;

public sealed class SubscriptionService(NexoraDbContext db,TimeProvider clock):ISubscriptionService
{
    private static readonly TimeSpan GracePeriod=TimeSpan.FromDays(7);
    public async Task<IReadOnlyList<SubscriptionView>> GetAllAsync(CancellationToken ct){await ProcessDueAsync(ct);return await Project(db.Subscriptions.OrderByDescending(x=>x.CreatedAt)).ToArrayAsync(ct);}
    public async Task<SubscriptionView?> GetAsync(Guid id,CancellationToken ct){await ProcessOneAsync(id,ct);return await Project(db.Subscriptions.Where(x=>x.Id==id)).SingleOrDefaultAsync(ct);}
    public async Task<SubscriptionView?> GetForTenantAsync(Guid tenantId,CancellationToken ct){var ids=await db.Subscriptions.Where(x=>x.TenantId==tenantId).Select(x=>x.Id).ToArrayAsync(ct);foreach(var id in ids)await ProcessOneAsync(id,ct);return await Project(db.Subscriptions.Where(x=>x.TenantId==tenantId).OrderByDescending(x=>x.CreatedAt)).FirstOrDefaultAsync(ct);}
    public async Task<IReadOnlyList<SubscriptionEventView>> GetEventsAsync(Guid id,CancellationToken ct)=>await db.SubscriptionEvents.Where(x=>x.SubscriptionId==id).OrderByDescending(x=>x.OccurredAt).Select(x=>new SubscriptionEventView(x.Id,x.ActorUserId,x.EventType,x.Details,x.OccurredAt)).ToArrayAsync(ct);
    public async Task<SubscriptionView> CreateTrialAsync(Guid actor,CreateSubscriptionInput input,string correlationId,CancellationToken ct)
    {
        if(!await db.Tenants.AnyAsync(x=>x.Id==input.TenantId&&x.IsActive,ct))throw new BillingValidationException("Tenant is unavailable.");if(!await db.Plans.AnyAsync(x=>x.Id==input.PlanId&&x.IsActive,ct))throw new BillingValidationException("Plan is unavailable.");
        await ProcessTenantAsync(input.TenantId,ct);if(await db.Subscriptions.AnyAsync(x=>x.TenantId==input.TenantId&&(x.Status==SubscriptionStatus.Trialing||x.Status==SubscriptionStatus.Active||x.Status==SubscriptionStatus.PastDue),ct))throw new BillingConflictException("Tenant already has a current subscription.");
        var now=clock.GetUtcNow();var value=new Subscription(input.TenantId,input.PlanId,input.BillingInterval,now,SubscriptionTrialPolicy.Duration);db.Add(value);Record(value,actor,"subscription.trial_started",$"planId={input.PlanId};interval={input.BillingInterval}",correlationId,now);await SaveAsync(ct);return (await GetAsync(value.Id,ct))!;
    }
    public Task<SubscriptionView?> ActivateAsync(Guid actor,Guid id,string correlationId,CancellationToken ct)=>MutateAsync(actor,id,"subscription.activated",correlationId,(x,now)=>x.Activate(now),ct);
    public async Task<SubscriptionView?> ChangePlanAsync(Guid actor,Guid id,Guid planId,string correlationId,CancellationToken ct){if(!await db.Plans.AnyAsync(x=>x.Id==planId&&x.IsActive,ct))throw new BillingValidationException("Plan is unavailable.");return await MutateAsync(actor,id,"subscription.plan_changed",correlationId,(x,now)=>x.ChangePlan(planId,now),ct,$"planId={planId}");}
    public Task<SubscriptionView?> ScheduleCancellationAsync(Guid actor,Guid id,string correlationId,CancellationToken ct)=>MutateAsync(actor,id,"subscription.cancellation_scheduled",correlationId,(x,now)=>x.ScheduleCancellation(now),ct);
    public Task<SubscriptionView?> CancelImmediatelyAsync(Guid actor,Guid id,string correlationId,CancellationToken ct)=>MutateAsync(actor,id,"subscription.canceled_immediately",correlationId,(x,now)=>x.CancelImmediately(now),ct);
    public Task<SubscriptionView?> MarkPastDueAsync(Guid actor,Guid id,string correlationId,CancellationToken ct)=>MutateAsync(actor,id,"subscription.marked_past_due",correlationId,(x,now)=>x.MarkPastDue(now),ct);
    public async Task<SubscriptionView?> ProcessAsync(Guid actor,Guid id,string correlationId,CancellationToken ct){var value=await db.Subscriptions.SingleOrDefaultAsync(x=>x.Id==id,ct);if(value is null)return null;var before=value.Status;if(value.Evaluate(clock.GetUtcNow(),GracePeriod)){Record(value,actor,"subscription.expired_or_canceled",$"from={before};to={value.Status}",correlationId,clock.GetUtcNow());await SaveAsync(ct);}return await GetAsync(id,ct);}
    private async Task<SubscriptionView?> MutateAsync(Guid actor,Guid id,string action,string correlationId,Action<Subscription,DateTimeOffset> mutation,CancellationToken ct,string? details=null){var value=await db.Subscriptions.SingleOrDefaultAsync(x=>x.Id==id,ct);if(value is null)return null;try{mutation(value,clock.GetUtcNow());}catch(InvalidOperationException ex){throw new BillingValidationException(ex.Message);}Record(value,actor,action,details,correlationId,clock.GetUtcNow());await SaveAsync(ct);return await GetAsync(id,ct);}
    // The detailed record lives in SubscriptionEvent (text). The AuditLog mirror is the cross-cutting
    // "who did what to which tenant" index (ADR-0021) — no Details, so the jsonb column stays clean.
    private void Record(Subscription value,Guid? actor,string action,string? details,string correlationId,DateTimeOffset now){var subscriptionEvent=new SubscriptionEvent(value.Id,actor,action,details,now);value.Events.Add(subscriptionEvent);db.SubscriptionEvents.Add(subscriptionEvent);if(actor.HasValue)db.AuditLogs.Add(new AuditLog(actor.Value,action,"Subscription",value.Id.ToString(),true,correlationId,now,tenantId:value.TenantId));}
    private async Task ProcessDueAsync(CancellationToken ct){var ids=await db.Subscriptions.Where(x=>x.Status==SubscriptionStatus.Trialing||x.Status==SubscriptionStatus.Active||x.Status==SubscriptionStatus.PastDue).Select(x=>x.Id).ToArrayAsync(ct);foreach(var id in ids)await ProcessOneAsync(id,ct);}
    private async Task ProcessTenantAsync(Guid tenantId,CancellationToken ct){var ids=await db.Subscriptions.Where(x=>x.TenantId==tenantId).Select(x=>x.Id).ToArrayAsync(ct);foreach(var id in ids)await ProcessOneAsync(id,ct);}
    private async Task ProcessOneAsync(Guid id,CancellationToken ct){var value=await db.Subscriptions.SingleOrDefaultAsync(x=>x.Id==id,ct);if(value is null)return;var before=value.Status;var now=clock.GetUtcNow();if(await PaidCoverageLifecycle.TryApplyAsync(db,value,now,ct)){Record(value,null,"subscription.paid_coverage_activated",$"from={before};to={value.Status}",string.Empty,now);await SaveAsync(ct);return;}if(value.Evaluate(now,GracePeriod)){Record(value,null,"subscription.lifecycle_evaluated",$"from={before};to={value.Status}",string.Empty,now);await SaveAsync(ct);}}
    private async Task SaveAsync(CancellationToken ct){try{await db.SaveChangesAsync(ct);}catch(DbUpdateException){throw new BillingConflictException("Subscription conflicts with the current tenant lifecycle.");}}
    private IQueryable<SubscriptionView> Project(IQueryable<Subscription> source)=>source.Join(db.Plans,x=>x.PlanId,p=>p.Id,(x,p)=>new SubscriptionView(x.Id,x.TenantId,x.PlanId,p.Code,x.Status,x.BillingInterval,x.TrialStartAt,x.TrialEndAt,x.CurrentPeriodStart,x.CurrentPeriodEnd,x.CancelAtPeriodEnd,x.CanceledAt,x.PastDueSince,x.CreatedAt,x.UpdatedAt));
}

public sealed class PersistentTenantPlanProvider(NexoraDbContext db,TimeProvider clock):ITenantPlanProvider
{
    private static readonly TimeSpan Grace=TimeSpan.FromDays(7);
    public async Task<Guid?> GetCurrentPlanIdAsync(Guid tenantId,CancellationToken ct)
    {
        var value=await db.Subscriptions.Where(x=>x.TenantId==tenantId&&(x.Status==SubscriptionStatus.Trialing||x.Status==SubscriptionStatus.Active||x.Status==SubscriptionStatus.PastDue)).OrderByDescending(x=>x.CreatedAt).SingleOrDefaultAsync(ct);if(value is null)return null;
        var now=clock.GetUtcNow();var before=value.Status;if(await PaidCoverageLifecycle.TryApplyAsync(db,value,now,ct))await db.SaveChangesAsync(ct);if(!value.GrantsEntitlement(now,Grace)){if(before!=value.Status){var subscriptionEvent=new SubscriptionEvent(value.Id,null,"subscription.lifecycle_evaluated",$"from={before};to={value.Status}",now);value.Events.Add(subscriptionEvent);db.SubscriptionEvents.Add(subscriptionEvent);await db.SaveChangesAsync(ct);}return null;}return value.PlanId;
    }
}
