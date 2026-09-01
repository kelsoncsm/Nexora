using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Nexora.Infrastructure.Persistence;

public sealed class NexoraDbContextFactory : IDesignTimeDbContextFactory<NexoraDbContext>
{
    public NexoraDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__NexoraDatabase")
            ?? throw new InvalidOperationException("ConnectionStrings__NexoraDatabase is required at design time.");
        var options = new DbContextOptionsBuilder<NexoraDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new NexoraDbContext(options);
    }
}
