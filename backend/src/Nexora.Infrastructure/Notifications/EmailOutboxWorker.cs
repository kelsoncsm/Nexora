using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Nexora.Infrastructure.Notifications;

public sealed class EmailOutboxWorker(IServiceScopeFactory scopeFactory, IOptions<EmailOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.WorkerEnabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.PollIntervalSeconds));
        do { await DrainAsync(stoppingToken); } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
    private async Task DrainAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            if (!await scope.ServiceProvider.GetRequiredService<EmailOutboxProcessor>().ProcessNextAsync(cancellationToken)) break;
        }
    }
}
