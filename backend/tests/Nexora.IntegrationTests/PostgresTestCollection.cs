namespace Nexora.IntegrationTests;

// Every PostgresApiFactory-backed class migrates the same real database in InitializeAsync.
// xUnit parallelizes test collections, so without a shared collection two such classes race
// on MigrateAsync. Keep all Postgres-backed classes in the "Postgres" collection to serialize them.
[CollectionDefinition("Postgres")]
public sealed class PostgresCollectionDefinition;
