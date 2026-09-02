using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Nexora.Infrastructure.Persistence.Migrations;

/// <summary>
/// Seeds the module feature catalog the runtime enforces (ADR-0019). These rows are a deploy
/// contract — the endpoint filters gate each module group on the matching code. Plans, plan
/// features, limits and prices remain created by the Platform Admin at runtime.
/// </summary>
[DbContext(typeof(NexoraDbContext))]
[Migration("20260902000000_SeedFeatureCatalog")]
public partial class SeedFeatureCatalog : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        INSERT INTO features ("Id", "Code", "Name", "IsActive", "CreatedAt") VALUES
          ('f0000000-0000-0000-0000-000000000001', 'CUSTOMERS',     'Clientes',      TRUE, NOW()),
          ('f0000000-0000-0000-0000-000000000002', 'SCHEDULING',    'Agenda',        TRUE, NOW()),
          ('f0000000-0000-0000-0000-000000000003', 'PROFESSIONALS', 'Profissionais', TRUE, NOW()),
          ('f0000000-0000-0000-0000-000000000004', 'SERVICES',      'Serviços',      TRUE, NOW()),
          ('f0000000-0000-0000-0000-000000000005', 'REPORTS',       'Relatórios',    TRUE, NOW())
        ON CONFLICT ("Code") DO NOTHING;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DELETE FROM features
        WHERE "Code" IN ('CUSTOMERS', 'SCHEDULING', 'PROFESSIONALS', 'SERVICES', 'REPORTS');
        """);
}
