using Nexora.Domain.Billing;

namespace Nexora.Application.Billing;

public sealed record CreateSubscriptionInput(Guid TenantId,Guid PlanId,BillingInterval BillingInterval);
public sealed record SubscriptionView(Guid Id,Guid TenantId,Guid PlanId,string PlanCode,SubscriptionStatus Status,BillingInterval BillingInterval,DateTimeOffset TrialStartAt,DateTimeOffset TrialEndAt,DateTimeOffset CurrentPeriodStart,DateTimeOffset CurrentPeriodEnd,bool CancelAtPeriodEnd,DateTimeOffset? CanceledAt,DateTimeOffset? PastDueSince,DateTimeOffset CreatedAt,DateTimeOffset UpdatedAt);
public sealed record SubscriptionEventView(Guid Id,Guid? ActorUserId,string EventType,string? Details,DateTimeOffset OccurredAt);
public interface ISubscriptionService
{
    Task<IReadOnlyList<SubscriptionView>> GetAllAsync(CancellationToken ct);
    Task<SubscriptionView?> GetAsync(Guid id,CancellationToken ct);
    Task<SubscriptionView?> GetForTenantAsync(Guid tenantId,CancellationToken ct);
    Task<IReadOnlyList<SubscriptionEventView>> GetEventsAsync(Guid id,CancellationToken ct);
    Task<SubscriptionView> CreateTrialAsync(Guid actorUserId,CreateSubscriptionInput input,string correlationId,CancellationToken ct);
    Task<SubscriptionView?> ActivateAsync(Guid actorUserId,Guid id,string correlationId,CancellationToken ct);
    Task<SubscriptionView?> ChangePlanAsync(Guid actorUserId,Guid id,Guid planId,string correlationId,CancellationToken ct);
    Task<SubscriptionView?> ScheduleCancellationAsync(Guid actorUserId,Guid id,string correlationId,CancellationToken ct);
    Task<SubscriptionView?> CancelImmediatelyAsync(Guid actorUserId,Guid id,string correlationId,CancellationToken ct);
    Task<SubscriptionView?> MarkPastDueAsync(Guid actorUserId,Guid id,string correlationId,CancellationToken ct);
    Task<SubscriptionView?> ProcessAsync(Guid actorUserId,Guid id,string correlationId,CancellationToken ct);
}
public sealed class BillingValidationException(string message):Exception(message);
public sealed class BillingConflictException(string message):Exception(message);
