namespace Nexora.Domain.Notifications;

public enum EmailOutboxStatus
{
    Pending,
    Processing,
    Sent,
    Failed
}

public sealed class EmailOutboxMessage
{
    private EmailOutboxMessage() { }

    public EmailOutboxMessage(
        Guid? tenantId,
        string type,
        string recipient,
        string templateKey,
        string payload,
        string idempotencyKey,
        DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Type = type;
        Recipient = recipient;
        TemplateKey = templateKey;
        Payload = payload;
        IdempotencyKey = idempotencyKey;
        Status = EmailOutboxStatus.Pending;
        NextAttemptAt = createdAt.ToUniversalTime();
        CreatedAt = createdAt.ToUniversalTime();
    }

    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Recipient { get; private set; } = string.Empty;
    public string TemplateKey { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public EmailOutboxStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ProcessingStartedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public string? LastError { get; private set; }
    public string? ExternalMessageId { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;

    public void Claim(DateTimeOffset now)
    {
        Status = EmailOutboxStatus.Processing;
        ProcessingStartedAt = now.ToUniversalTime();
        AttemptCount++;
        LastError = null;
    }

    public void MarkSent(string? externalMessageId, DateTimeOffset now)
    {
        Status = EmailOutboxStatus.Sent;
        ExternalMessageId = externalMessageId;
        ProcessedAt = now.ToUniversalTime();
        ProcessingStartedAt = null;
        LastError = null;
    }

    public void ScheduleRetry(DateTimeOffset nextAttemptAt, string error)
    {
        Status = EmailOutboxStatus.Pending;
        NextAttemptAt = nextAttemptAt.ToUniversalTime();
        ProcessingStartedAt = null;
        LastError = Limit(error);
    }

    public void MarkFailed(string error, DateTimeOffset now)
    {
        Status = EmailOutboxStatus.Failed;
        ProcessedAt = now.ToUniversalTime();
        ProcessingStartedAt = null;
        LastError = Limit(error);
    }

    /// <summary>
    /// Drops the payload once the message reaches a terminal state. Used for messages whose payload
    /// holds a one-time secret (e.g. an invitation-accept link) that must not live on as permanent
    /// data. The template key stays, so the row is still identifiable for audit/metrics.
    /// </summary>
    public void RedactPayload() => Payload = "{}";

    public void ReleaseStaleClaim(DateTimeOffset now)
    {
        Status = EmailOutboxStatus.Pending;
        NextAttemptAt = now.ToUniversalTime();
        ProcessingStartedAt = null;
        LastError = "Processing lease expired.";
    }

    private static string Limit(string value) => value.Length <= 1000 ? value : value[..1000];
}
