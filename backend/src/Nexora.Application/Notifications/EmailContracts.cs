namespace Nexora.Application.Notifications;

public sealed record EmailMessage(
    string Recipient,
    string Subject,
    string HtmlBody,
    string IdempotencyKey,
    string TemplateKey);

public sealed record EmailSendResult(string? ExternalMessageId);

public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

public interface IEmailOutbox
{
    void EnqueueWelcome(Guid userId, Guid? tenantId, string recipient, string displayName);

    /// <summary>
    /// Enqueues the tenant-invitation e-mail. <paramref name="acceptUrl"/> contains the raw
    /// invitation token; it is stored in the outbox payload only until the message is sent, after
    /// which the processor redacts the payload — the token is never permanent data.
    /// </summary>
    void EnqueueTenantInvitation(
        Guid invitationId, Guid tenantId, string recipient,
        string tenantName, string roleName, string inviterEmail, string acceptUrl);
}

public enum EmailFailureKind
{
    Transient,
    Permanent
}

public sealed class EmailProviderException(string message, EmailFailureKind kind, Exception? innerException = null)
    : Exception(message, innerException)
{
    public EmailFailureKind Kind { get; } = kind;
}
