using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nexora.Application.Notifications;
using Nexora.Domain.Notifications;
using Nexora.Infrastructure.Persistence;

namespace Nexora.Infrastructure.Notifications;

public sealed class EmailOutboxProcessor(NexoraDbContext dbContext, IEmailSender emailSender,
    IOptions<EmailOptions> options, TimeProvider timeProvider, ILogger<EmailOutboxProcessor> logger)
{
    private static readonly Action<ILogger, Guid, int, Exception?> LogSending = LoggerMessage.Define<Guid, int>(LogLevel.Information, new EventId(1, "EmailOutboxSending"), "Processing email outbox message {OutboxMessageId}, attempt {AttemptCount}");
    private static readonly Action<ILogger, Guid, string?, Exception?> LogSent = LoggerMessage.Define<Guid, string?>(LogLevel.Information, new EventId(2, "EmailOutboxSent"), "Email outbox message {OutboxMessageId} sent as {ExternalMessageId}");
    private static readonly Action<ILogger, Guid, int, Exception?> LogRetry = LoggerMessage.Define<Guid, int>(LogLevel.Warning, new EventId(3, "EmailOutboxRetry"), "Email outbox message {OutboxMessageId} scheduled for retry after attempt {AttemptCount}");
    private static readonly Action<ILogger, Guid, int, Exception?> LogFailed = LoggerMessage.Define<Guid, int>(LogLevel.Error, new EventId(4, "EmailOutboxFailed"), "Email outbox message {OutboxMessageId} failed permanently after attempt {AttemptCount}");

    public async Task<bool> ProcessNextAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await ReleaseStaleClaimsAsync(now, cancellationToken);
        var candidateId = await dbContext.EmailOutboxMessages.Where(x => x.Status == EmailOutboxStatus.Pending && x.NextAttemptAt <= now)
            .OrderBy(x => x.CreatedAt).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(cancellationToken);
        if (!candidateId.HasValue) return false;
        if (!await TryClaimAsync(candidateId.Value, now, cancellationToken)) return true;
        var message = await dbContext.EmailOutboxMessages.SingleAsync(x => x.Id == candidateId.Value, cancellationToken);
        LogSending(logger, message.Id, message.AttemptCount, null);
        try
        {
            var result = await emailSender.SendAsync(BuildEmail(message), cancellationToken);
            message.MarkSent(result.ExternalMessageId, timeProvider.GetUtcNow());
            await dbContext.SaveChangesAsync(cancellationToken);
            LogSent(logger, message.Id, result.ExternalMessageId, null);
        }
        catch (EmailProviderException exception)
        {
            var settings = options.Value;
            if (exception.Kind == EmailFailureKind.Permanent || message.AttemptCount >= settings.MaxAttempts)
            { message.MarkFailed(exception.Message, timeProvider.GetUtcNow()); LogFailed(logger, message.Id, message.AttemptCount, exception); }
            else
            {
                var index = Math.Min(message.AttemptCount - 1, settings.RetryDelaysSeconds.Length - 1);
                message.ScheduleRetry(timeProvider.GetUtcNow().AddSeconds(settings.RetryDelaysSeconds[Math.Max(0, index)]), exception.Message);
                LogRetry(logger, message.Id, message.AttemptCount, exception);
            }
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        return true;
    }

    private async Task<bool> TryClaimAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (string.Equals(dbContext.Database.ProviderName, "Npgsql.EntityFrameworkCore.PostgreSQL", StringComparison.Ordinal))
        {
            var affected = await dbContext.EmailOutboxMessages.Where(x => x.Id == id && x.Status == EmailOutboxStatus.Pending && x.NextAttemptAt <= now)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, EmailOutboxStatus.Processing)
                    .SetProperty(x => x.ProcessingStartedAt, now).SetProperty(x => x.AttemptCount, x => x.AttemptCount + 1)
                    .SetProperty(x => x.LastError, (string?)null), cancellationToken);
            dbContext.ChangeTracker.Clear(); return affected == 1;
        }
        var message = await dbContext.EmailOutboxMessages.SingleOrDefaultAsync(x => x.Id == id && x.Status == EmailOutboxStatus.Pending && x.NextAttemptAt <= now, cancellationToken);
        if (message is null) return false;
        message.Claim(now); await dbContext.SaveChangesAsync(cancellationToken); dbContext.ChangeTracker.Clear(); return true;
    }

    private async Task ReleaseStaleClaimsAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var staleBefore = now.AddMinutes(-options.Value.ProcessingLeaseMinutes);
        var stale = await dbContext.EmailOutboxMessages.Where(x => x.Status == EmailOutboxStatus.Processing && x.ProcessingStartedAt < staleBefore).ToListAsync(cancellationToken);
        foreach (var message in stale) message.ReleaseStaleClaim(now);
        if (stale.Count > 0) await dbContext.SaveChangesAsync(cancellationToken);
    }

    private EmailMessage BuildEmail(EmailOutboxMessage message)
    {
        if (message.TemplateKey != WelcomeEmailTemplate.Key) throw new EmailProviderException("Unknown email template.", EmailFailureKind.Permanent);
        var payload = JsonSerializer.Deserialize<EmailOutbox.WelcomePayload>(message.Payload) ?? throw new EmailProviderException("Invalid email payload.", EmailFailureKind.Permanent);
        var rendered = WelcomeEmailTemplate.Render(payload.DisplayName, options.Value.ApplicationUrl);
        return new(message.Recipient, rendered.Subject, rendered.HtmlBody, message.IdempotencyKey, message.TemplateKey);
    }
}
