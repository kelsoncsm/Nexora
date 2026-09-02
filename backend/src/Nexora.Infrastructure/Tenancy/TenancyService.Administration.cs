using Microsoft.EntityFrameworkCore;
using Nexora.Application.Administration;
using Nexora.Application.Tenancy;
using Nexora.Domain.Tenancy;

namespace Nexora.Infrastructure.Tenancy;

public sealed partial class TenancyService
{
    // ---- Company profile -------------------------------------------------------------------

    public Task<TenantProfile?> GetProfileAsync(Guid tenantId, CancellationToken ct) =>
        dbContext.Tenants.Where(x => x.Id == tenantId)
            .Select(x => new TenantProfile(x.Id, x.Name, x.Slug, x.TimeZoneId, x.IsActive, x.CreatedAt))
            .SingleOrDefaultAsync(ct);

    public async Task<TenantProfile> UpdateProfileAsync(Guid tenantId, string name, string timeZoneId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length is < 2 or > 200)
            throw new TenantValidationException("Tenant name must contain between 2 and 200 characters.");
        if (string.IsNullOrWhiteSpace(timeZoneId) || !TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId.Trim(), out var zone) || !zone.HasIanaId)
            throw new TenantValidationException("Tenant TimeZoneId must be a valid IANA identifier.");

        var tenant = await dbContext.Tenants.SingleOrDefaultAsync(x => x.Id == tenantId, ct)
            ?? throw new TenantValidationException("Tenant not found.");
        tenant.UpdateProfile(name.Trim(), zone.Id);
        await dbContext.SaveChangesAsync(ct);
        return new TenantProfile(tenant.Id, tenant.Name, tenant.Slug, tenant.TimeZoneId, tenant.IsActive, tenant.CreatedAt);
    }

    // ---- Roles & permissions -------------------------------------------------------------

    public async Task<IReadOnlyCollection<TenantRoleView>> GetRolesAsync(Guid tenantId, CancellationToken ct) =>
        await dbContext.TenantRoles.Where(x => x.TenantId == tenantId).OrderByDescending(x => x.IsSystem).ThenBy(x => x.Name)
            .Select(x => new TenantRoleView(x.Id, x.Name, x.Description, x.IsSystem,
                x.Users.Count(u => u.IsActive),
                x.Permissions.Select(p => p.PermissionKey).ToArray()))
            .ToArrayAsync(ct);

    public Task<TenantRoleView?> GetRoleAsync(Guid tenantId, Guid roleId, CancellationToken ct) =>
        dbContext.TenantRoles.Where(x => x.TenantId == tenantId && x.Id == roleId)
            .Select(x => new TenantRoleView(x.Id, x.Name, x.Description, x.IsSystem,
                x.Users.Count(u => u.IsActive),
                x.Permissions.Select(p => p.PermissionKey).ToArray()))
            .SingleOrDefaultAsync(ct);

    public async Task<TenantRoleView> CreateRoleAsync(Guid tenantId, string name, string description, IReadOnlyCollection<string> permissions, CancellationToken ct)
    {
        var cleanName = (name ?? string.Empty).Trim();
        if (cleanName.Length is < 2 or > 100) throw new TenantValidationException("Role name must contain between 2 and 100 characters.");
        var cleanDescription = (description ?? string.Empty).Trim();
        if (cleanDescription.Length > 300) throw new TenantValidationException("Role description must not exceed 300 characters.");
        var keys = ValidatePermissionKeys(permissions);
        if (await dbContext.TenantRoles.AnyAsync(x => x.TenantId == tenantId && x.Name == cleanName, ct))
            throw new TenantConflictException("A role with this name already exists.");

        var role = new TenantRole(tenantId, cleanName, cleanDescription, false);
        foreach (var key in keys) role.Permissions.Add(new TenantRolePermission(role.Id, key));
        dbContext.TenantRoles.Add(role);
        await dbContext.SaveChangesAsync(ct);
        return new TenantRoleView(role.Id, role.Name, role.Description, role.IsSystem, 0, keys);
    }

    public async Task<TenantRoleView?> UpdateRoleAsync(Guid tenantId, Guid roleId, string name, string description, CancellationToken ct)
    {
        var role = await dbContext.TenantRoles.Include(x => x.Permissions)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == roleId, ct);
        if (role is null) return null;
        if (role.IsSystem) throw new TenantValidationException("System roles cannot be modified.");
        var cleanName = (name ?? string.Empty).Trim();
        if (cleanName.Length is < 2 or > 100) throw new TenantValidationException("Role name must contain between 2 and 100 characters.");
        var cleanDescription = (description ?? string.Empty).Trim();
        if (cleanDescription.Length > 300) throw new TenantValidationException("Role description must not exceed 300 characters.");
        if (await dbContext.TenantRoles.AnyAsync(x => x.TenantId == tenantId && x.Name == cleanName && x.Id != roleId, ct))
            throw new TenantConflictException("A role with this name already exists.");

        role.Update(cleanName, cleanDescription);
        await dbContext.SaveChangesAsync(ct);
        return new TenantRoleView(role.Id, role.Name, role.Description, role.IsSystem,
            await dbContext.TenantUsers.CountAsync(u => u.TenantRoleId == role.Id && u.IsActive, ct),
            role.Permissions.Select(p => p.PermissionKey).ToArray());
    }

    public async Task<TenantRoleView?> SetRolePermissionsAsync(Guid tenantId, Guid actorUserId, Guid roleId, IReadOnlyCollection<string> permissions, string correlationId, CancellationToken ct)
    {
        var role = await dbContext.TenantRoles.Include(x => x.Permissions)
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == roleId, ct);
        if (role is null) return null;
        if (role.IsSystem) throw new TenantValidationException("System role permissions cannot be modified.");
        var keys = ValidatePermissionKeys(permissions);
        var before = role.Permissions.Select(p => p.PermissionKey).ToHashSet(StringComparer.Ordinal);
        var added = keys.Where(k => !before.Contains(k)).ToArray();
        var removed = before.Where(k => !keys.Contains(k, StringComparer.Ordinal)).ToArray();
        role.ReplacePermissions(keys);
        if (added.Length > 0 || removed.Length > 0)
            audit.Record(actorUserId, AuditActions.RolePermissionsChanged, "TenantRole", roleId.ToString(), correlationId, tenantId,
                new { roleId, added, removed });
        await dbContext.SaveChangesAsync(ct);
        return new TenantRoleView(role.Id, role.Name, role.Description, role.IsSystem,
            await dbContext.TenantUsers.CountAsync(u => u.TenantRoleId == role.Id && u.IsActive, ct), keys);
    }

    private static string[] ValidatePermissionKeys(IReadOnlyCollection<string>? permissions)
    {
        var keys = (permissions ?? []).Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.Ordinal).ToArray();
        var unknown = keys.Where(k => !TenantPermissions.All.Contains(k, StringComparer.Ordinal)).ToArray();
        if (unknown.Length > 0) throw new TenantValidationException($"Unknown permission keys: {string.Join(", ", unknown)}.");
        return keys;
    }
}
