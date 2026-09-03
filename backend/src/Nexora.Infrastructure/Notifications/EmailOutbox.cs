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

    public void EnqueueTenantInvitation(
        Guid invitationId, Guid tenantId, string recipient,
        string tenantName, string roleName, string inviterEmail, string acceptUrl)
    {
        var payload = JsonSerializer.Serialize(new TenantInvitationPayload(tenantName, roleName, inviterEmail, acceptUrl));
        // Idempotency key includes the invitation id so a resend (new invitation-less row? no — same
        // invitation, rotated token) still enqueues a fresh message: use a per-send suffix.
        dbContext.EmailOutboxMessages.Add(new EmailOutboxMessage(
            tenantId,
            TenantInvitationEmailTemplate.Key,
            recipient,
            TenantInvitationEmailTemplate.Key,
            payload,
            $"tenant-invitation:{invitationId:N}:{timeProvider.GetUtcNow().ToUnixTimeMilliseconds()}",
            timeProvider.GetUtcNow()));
    }

    internal sealed record WelcomePayload(string DisplayName);
    internal sealed record TenantInvitationPayload(string TenantName, string RoleName, string InviterEmail, string AcceptUrl);
}
