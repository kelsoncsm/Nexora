using System.Collections.Concurrent;
using Nexora.Application.Notifications;

namespace Nexora.Infrastructure.Notifications;

public enum FakeEmailBehavior
{
    Success,
    Timeout,
    RateLimited,
    ProviderError
}

public sealed class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> messages = new();
    public FakeEmailBehavior Behavior { get; set; }
    public IReadOnlyCollection<EmailMessage> Messages => messages.ToArray();

    public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Behavior == FakeEmailBehavior.Timeout)
            throw new EmailProviderException("Email provider timed out.", EmailFailureKind.Transient, new TimeoutException());
        if (Behavior == FakeEmailBehavior.RateLimited)
            throw new EmailProviderException("Email provider rate limit reached.", EmailFailureKind.Transient);
        if (Behavior == FakeEmailBehavior.ProviderError)
            throw new EmailProviderException("Email provider rejected the message.", EmailFailureKind.Permanent);
        messages.Enqueue(message);
        return Task.FromResult(new EmailSendResult($"fake-{messages.Count}"));
    }
}
