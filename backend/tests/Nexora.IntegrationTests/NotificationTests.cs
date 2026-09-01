using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nexora.Domain.Notifications;
using Nexora.Infrastructure.Notifications;
using Nexora.Infrastructure.Persistence;

namespace Nexora.IntegrationTests;

public sealed class NotificationTests
{
    [Fact]
    public async Task RegistrationPersistsWelcomeMessageWithoutCallingProvider()
    {
        await using var factory = new ApiFactory();
        var fake = factory.Services.GetRequiredService<FakeEmailSender>();
        var before = fake.Messages.Count;
        using var client = factory.CreateClient();
        var email = $"welcome-{Guid.NewGuid():N}@nexora.test";

        var response = await client.PostAsJsonAsync("/api/v1/identity/register", new { email, password = "Correct-Horse-42" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(before, fake.Messages.Count);
        using var scope = factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<NexoraDbContext>().EmailOutboxMessages.SingleAsync(x => x.Recipient == email);
        Assert.Equal(EmailOutboxStatus.Pending, stored.Status);
        Assert.StartsWith("welcome:", stored.IdempotencyKey, StringComparison.Ordinal);
        Assert.DoesNotContain("Correct-Horse-42", stored.Payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProcessorSendsPendingMessageOnceAndMarksItSent()
    {
        await using var factory = new ApiFactory();
        var fake = factory.Services.GetRequiredService<FakeEmailSender>();
        fake.Behavior = FakeEmailBehavior.Success;
        var before = fake.Messages.Count;
        Guid id;
        await using (var seedScope = factory.Services.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<NexoraDbContext>();
            var message = new EmailOutboxMessage(null, "WelcomeEmail", "worker@nexora.test", "WelcomeEmail", "{\"DisplayName\":\"Worker\"}", $"worker:{Guid.NewGuid():N}", DateTimeOffset.UtcNow.AddYears(-1));
            id = message.Id; db.EmailOutboxMessages.Add(message); await db.SaveChangesAsync();
        }

        await using (var processScope = factory.Services.CreateAsyncScope())
            Assert.True(await processScope.ServiceProvider.GetRequiredService<EmailOutboxProcessor>().ProcessNextAsync(CancellationToken.None));
        await using (var secondScope = factory.Services.CreateAsyncScope())
            Assert.False(await secondScope.ServiceProvider.GetRequiredService<EmailOutboxProcessor>().ProcessNextAsync(CancellationToken.None));
        Assert.Equal(before + 1, fake.Messages.Count);
        await using var assertScope = factory.Services.CreateAsyncScope();
        var stored = await assertScope.ServiceProvider.GetRequiredService<NexoraDbContext>().EmailOutboxMessages.SingleAsync(x => x.Id == id);
        Assert.Equal(EmailOutboxStatus.Sent, stored.Status);
        Assert.Equal(1, stored.AttemptCount);
        Assert.DoesNotContain("password", fake.Messages.Last().HtmlBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", fake.Messages.Last().HtmlBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TransientAndPermanentFailuresAreClassified()
    {
        await using var factory = new ApiFactory();
        var fake = factory.Services.GetRequiredService<FakeEmailSender>();
        var transientId = await SeedAsync(factory, "transient"); fake.Behavior = FakeEmailBehavior.Timeout;
        await ProcessAsync(factory);
        var transient = await LoadAsync(factory, transientId);
        Assert.Equal(EmailOutboxStatus.Pending, transient.Status);
        Assert.Equal(1, transient.AttemptCount);

        var permanentId = await SeedAsync(factory, "permanent"); fake.Behavior = FakeEmailBehavior.ProviderError;
        await ProcessAsync(factory);
        var permanent = await LoadAsync(factory, permanentId);
        Assert.Equal(EmailOutboxStatus.Failed, permanent.Status);
        fake.Behavior = FakeEmailBehavior.Success;
    }

    private static async Task<Guid> SeedAsync(ApiFactory factory, string prefix)
    {
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        var message = new EmailOutboxMessage(null, "WelcomeEmail", $"{prefix}@nexora.test", "WelcomeEmail", "{\"DisplayName\":\"Test\"}", $"{prefix}:{Guid.NewGuid():N}", DateTimeOffset.UtcNow.AddYears(-1));
        db.EmailOutboxMessages.Add(message); await db.SaveChangesAsync(); return message.Id;
    }
    private static async Task ProcessAsync(ApiFactory factory) { await using var scope = factory.Services.CreateAsyncScope(); await scope.ServiceProvider.GetRequiredService<EmailOutboxProcessor>().ProcessNextAsync(CancellationToken.None); }
    private static async Task<EmailOutboxMessage> LoadAsync(ApiFactory factory, Guid id) { await using var scope = factory.Services.CreateAsyncScope(); return await scope.ServiceProvider.GetRequiredService<NexoraDbContext>().EmailOutboxMessages.AsNoTracking().SingleAsync(x => x.Id == id); }
}
