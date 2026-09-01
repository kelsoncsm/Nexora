using Nexora.Domain.Notifications;

namespace Nexora.UnitTests.Notifications;

public sealed class EmailOutboxMessageTests
{
    [Fact]
    public void MessagePreservesIdempotencyAndTransitionsToSent()
    {
        var now = DateTimeOffset.UtcNow;
        var message = new EmailOutboxMessage(null, "WelcomeEmail", "user@example.test", "WelcomeEmail", "{}", "welcome:123", now);

        message.Claim(now);
        message.MarkSent("provider-1", now.AddSeconds(1));

        Assert.Equal("welcome:123", message.IdempotencyKey);
        Assert.Equal(EmailOutboxStatus.Sent, message.Status);
        Assert.Equal(1, message.AttemptCount);
        Assert.Equal("provider-1", message.ExternalMessageId);
    }

    [Fact]
    public void TransientFailureReturnsMessageToPending()
    {
        var now = DateTimeOffset.UtcNow;
        var message = new EmailOutboxMessage(Guid.NewGuid(), "WelcomeEmail", "user@example.test", "WelcomeEmail", "{}", "welcome:456", now);
        message.Claim(now);

        message.ScheduleRetry(now.AddMinutes(1), "timeout");

        Assert.Equal(EmailOutboxStatus.Pending, message.Status);
        Assert.Equal(now.AddMinutes(1), message.NextAttemptAt);
        Assert.Equal("timeout", message.LastError);
    }
}
