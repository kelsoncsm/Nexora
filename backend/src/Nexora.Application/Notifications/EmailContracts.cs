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
