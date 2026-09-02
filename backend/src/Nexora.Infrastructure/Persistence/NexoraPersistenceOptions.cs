using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Nexora.Infrastructure.Persistence;

/// <summary>
/// Which PostgreSQL schema <see cref="NexoraDbContext"/> maps its entities and migrations history to
/// (ADR-0022). Defaults to <see cref="DatabaseSchemas.Application"/>; only the integration-test host
/// sets <see cref="DatabaseSchemas.IntegrationTests"/>.
/// </summary>
public sealed class NexoraPersistenceOptions
{
    public string Schema { get; set; } = DatabaseSchemas.Application;
}

/// <summary>
/// Keys the EF model cache by the active schema so a process that builds the model for more than one
/// schema (or design-time vs runtime) does not reuse the wrong compiled model.
/// </summary>
public sealed class SchemaAwareModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
        => (context.GetType(), (context as NexoraDbContext)?.Schema, designTime);
}
