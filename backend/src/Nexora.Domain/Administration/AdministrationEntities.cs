namespace Nexora.Domain.Administration;

public sealed class BusinessSegment
{
    private BusinessSegment() { }
    public BusinessSegment(string code, string name, DateTimeOffset createdAt)
    { Id = Guid.NewGuid(); Code = code; Name = name; IsActive = true; CreatedAt = createdAt; }
    public Guid Id { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public void Update(string name, bool isActive) { Name = name; IsActive = isActive; }
}

public sealed class AuditLog
{
    private AuditLog() { }
    public AuditLog(Guid actorUserId, string action, string targetType, string targetId,
        bool succeeded, string correlationId, DateTimeOffset occurredAt,
        Guid? tenantId = null, string? details = null)
    { Id = Guid.NewGuid(); ActorUserId = actorUserId; Action = action; TargetType = targetType;
      TargetId = targetId; Succeeded = succeeded; CorrelationId = correlationId; OccurredAt = occurredAt;
      TenantId = tenantId; Details = details; }
    public Guid Id { get; private set; }
    public Guid ActorUserId { get; private set; }
    /// <summary>The tenant the action acted on. Null for platform-global actions and pre-P2.9 rows.</summary>
    public Guid? TenantId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string TargetType { get; private set; } = string.Empty;
    public string TargetId { get; private set; } = string.Empty;
    public bool Succeeded { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    /// <summary>Structured JSON (jsonb): ids and codes only, never secrets or unnecessary PII.</summary>
    public string? Details { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
}
