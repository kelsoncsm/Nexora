using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Options;

namespace Nexora.Infrastructure.Persistence;

public sealed class NexoraDbContextFactory : IDesignTimeDbContextFactory<NexoraDbContext>
{
    public NexoraDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__NexoraDatabase")
            ?? throw new InvalidOperationException("ConnectionStrings__NexoraDatabase is required at design time.");
        var schema = Environment.GetEnvironmentVariable("Database__Schema") ?? DatabaseSchemas.Application;

        var options = new DbContextOptionsBuilder<NexoraDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable(DatabaseSchemas.MigrationsHistoryTable, schema))
            .Options;
        return new NexoraDbContext(options, Options.Create(new NexoraPersistenceOptions { Schema = schema }));
    }
}
