using Microsoft.EntityFrameworkCore;
using Nexora.Infrastructure.Persistence;

namespace Nexora.Api.Configuration;

public static class DatabaseInitialization
{
    public static async Task ApplyDevelopmentMigrationsAsync(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) return;
        await using var scope = app.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<NexoraDbContext>();
        if (!context.Database.IsRelational()) return;

        // ADR-0022: the application owns exactly one schema in the shared saas_dev database.
        // Pre-create it (guarded to the two Nexora-owned schemas) so the schema-relative
        // historical migrations land in the right place on a fresh database.
        await NexoraSchemaGuard.CreateIfMissingAsync(context.Database, context.Schema);
        await context.Database.MigrateAsync();
    }
}
