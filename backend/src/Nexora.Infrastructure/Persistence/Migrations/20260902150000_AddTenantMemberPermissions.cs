using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Nexora.Infrastructure.Persistence.Migrations;

/// <summary>
/// Adds CRUD granularity for member management (<c>tenant.members.{read,create,update,delete}</c>,
/// approved 2026-09-02). Additive backfill: every tenant role that currently holds
/// <c>tenant.manage</c> — which is every system ADMIN role and any custom role granted company
/// administration — receives the four new keys, so no tenant loses the ability to manage members.
/// <para><b>tenant.manage is not removed from any role.</b> Splitting the remaining
/// <c>tenant.manage</c> surface (profile / roles / billing) is a separate, still-open decision
/// (see <c>docs/NEXORA-UI-COVERAGE.md §7</c>).</para>
/// </summary>
[DbContext(typeof(NexoraDbContext))]
[Migration("20260902150000_AddTenantMemberPermissions")]
public partial class AddTenantMemberPermissions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        INSERT INTO tenant_role_permissions ("TenantRoleId", "PermissionKey")
        SELECT trp."TenantRoleId", k.key
        FROM tenant_role_permissions trp
        CROSS JOIN (VALUES
            ('tenant.members.read'),
            ('tenant.members.create'),
            ('tenant.members.update'),
            ('tenant.members.delete')
        ) AS k(key)
        WHERE trp."PermissionKey" = 'tenant.manage'
        ON CONFLICT DO NOTHING;
        """);

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DELETE FROM tenant_role_permissions WHERE "PermissionKey" LIKE 'tenant.members.%';
        """);
}
