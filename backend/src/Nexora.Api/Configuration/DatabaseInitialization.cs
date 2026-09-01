using Microsoft.EntityFrameworkCore;
using Nexora.Infrastructure.Persistence;

namespace Nexora.Api.Configuration;

public static class DatabaseInitialization
{
    public static async Task ApplyDevelopmentMigrationsAsync(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment()) return;
        await using var scope = app.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<NexoraDbContext>().Database;
        if (database.IsRelational()) await database.MigrateAsync();
    }
}
