using System.Text.Json;
using Microsoft.Extensions.Options;
using Nexora.Application.Notifications;
using Nexora.Domain.Notifications;
using Nexora.Infrastructure.Persistence;

namespace Nexora.Infrastructure.Notifications;

public sealed class EmailOutbox(
    NexoraDbContext dbContext,
    TimeProvider timeProvider) : IEmailOutbox
{
    public void EnqueueWelcome(Guid userId, Guid? tenantId, string recipient, string displayName)
    {
        var payload = JsonSerializer.Serialize(new WelcomePayload(displayName));
        dbContext.EmailOutboxMessages.Add(new EmailOutboxMessage(
            tenantId,
            WelcomeEmailTemplate.Key,
            recipient,
            WelcomeEmailTemplate.Key,
            payload,
            $"welcome:{userId:N}",
            timeProvider.GetUtcNow()));
    }

    internal sealed record WelcomePayload(string DisplayName);
}
