using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Nexora.Infrastructure.Persistence.Migrations;

[DbContext(typeof(NexoraDbContext))]
[Migration("20260901190000_SeedBarbershopSalonVertical")]
public partial class SeedBarbershopSalonVertical : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        INSERT INTO business_segments ("Id", "Code", "Name", "IsActive", "CreatedAt")
        VALUES ('f1500000-0000-0000-0000-000000000001', 'BARBERSHOP_SALON', 'Barbearia / Salão', TRUE, NOW())
        ON CONFLICT ("Code") DO NOTHING;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DELETE FROM business_segments
        WHERE "Id" = 'f1500000-0000-0000-0000-000000000001'
          AND "Code" = 'BARBERSHOP_SALON';
        """);
}
