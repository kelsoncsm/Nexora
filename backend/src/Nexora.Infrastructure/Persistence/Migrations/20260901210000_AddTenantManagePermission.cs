using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

#nullable disable

namespace Nexora.Infrastructure.Persistence.Migrations;

/// <summary>
/// Backfills the <c>tenant.manage</c> permission into every system (ADMIN) tenant role so existing
/// tenants keep a member able to administer company profile, roles and permissions.
/// </summary>
[DbContext(typeof(NexoraDbContext))]
[Migration("20260901210000_AddTenantManagePermission")]
public partial class BackfillTenantManageAccess : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        INSERT INTO tenant_role_permissions ("TenantRoleId", "PermissionKey")
        SELECT "Id", 'tenant.manage'
        FROM tenant_roles
        WHERE "IsSystem" = TRUE
        ON CONFLICT DO NOTHING;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DELETE FROM tenant_role_permissions WHERE "PermissionKey" = 'tenant.manage';
        """);
}
