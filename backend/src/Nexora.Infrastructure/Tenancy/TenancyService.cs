using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Nexora.Application.Administration;
using Nexora.Application.Tenancy;
using Nexora.Domain.Tenancy;
using Nexora.Infrastructure.Persistence;

namespace Nexora.Infrastructure.Tenancy;

public sealed partial class TenancyService(NexoraDbContext dbContext, TimeProvider timeProvider, IAuditLogWriter audit) : ITenancyService
{
    public async Task<PublicTenant?> ResolvePublicAsync(string slug, CancellationToken ct)
    {
        var normalized = NormalizeSlug(slug);
        return await dbContext.Tenants.Where(x => x.Slug == normalized && x.IsActive)
            .Select(x => new PublicTenant(x.Id, x.Name, x.Slug, x.TimeZoneId)).SingleOrDefaultAsync(ct);
    }

    public async Task<PublicTenant> CreateAsync(Guid userId, string name, string slug, string timeZoneId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 200) throw new TenantValidationException("Tenant name is invalid.");
        var normalized = NormalizeSlug(slug);
        if (await dbContext.Tenants.AnyAsync(x => x.Slug == normalized, ct)) throw new TenantConflictException("Tenant slug is unavailable.");
        if (string.IsNullOrWhiteSpace(timeZoneId) || !TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId.Trim(), out var zone) || !zone.HasIanaId)
            throw new TenantValidationException("Tenant TimeZoneId must be a valid IANA identifier.");
        var tenant = new Tenant(name.Trim(), normalized, zone.Id, timeProvider.GetUtcNow());
        tenant.AddInitialAdministrator(userId, TenantPermissions.All, timeProvider.GetUtcNow());
        dbContext.Tenants.Add(tenant); await dbContext.SaveChangesAsync(ct);
        return new PublicTenant(tenant.Id, tenant.Name, tenant.Slug, tenant.TimeZoneId);
    }

    public Task<bool> ValidateMembershipAsync(Guid tenantId, Guid userId, CancellationToken ct) =>
        dbContext.TenantUsers.AnyAsync(x => x.TenantId == tenantId && x.UserId == userId && x.IsActive && x.Tenant.IsActive, ct);

    public async Task<IReadOnlyCollection<string>> GetPermissionsAsync(Guid tenantId,Guid userId,CancellationToken ct)=>await dbContext.TenantUsers.Where(x=>x.TenantId==tenantId&&x.UserId==userId&&x.IsActive&&x.Tenant.IsActive).SelectMany(x=>x.TenantRole.Permissions).Select(x=>x.PermissionKey).Distinct().ToArrayAsync(ct);

    public async Task<IReadOnlyCollection<UserTenant>> GetUserTenantsAsync(Guid userId, CancellationToken ct) =>
        await dbContext.TenantUsers
            .Where(x => x.UserId == userId && x.IsActive && x.Tenant.IsActive)
            .OrderBy(x => x.Tenant.Name)
            .Select(x => new UserTenant(x.Tenant.Id, x.Tenant.Name, x.Tenant.Slug, x.TenantRole.Name))
            .ToArrayAsync(ct);

    public Task<Guid?> ResolveMembershipTenantAsync(Guid userId, string slug, CancellationToken ct)
    {
        var normalized = NormalizeSlug(slug);
        return dbContext.TenantUsers.Where(x => x.UserId == userId && x.IsActive && x.Tenant.IsActive && x.Tenant.Slug == normalized)
            .Select(x => (Guid?)x.TenantId).SingleOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyCollection<TenantMembership>> GetMembersAsync(Guid tenantId, CancellationToken ct) =>
        await dbContext.TenantUsers.Where(x => x.TenantId == tenantId)
            .Select(x => new TenantMembership(x.Id, x.UserId, x.User.Email, x.TenantRole.Name, x.TenantRoleId, x.IsActive)).ToArrayAsync(ct);

    public async Task<bool> DeactivateMembershipAsync(Guid tenantId, Guid actorUserId, Guid membershipId, string correlationId, CancellationToken ct)
    {
        // Defence in depth behind the endpoint's tenant.members.delete filter.
        var canManage = await dbContext.TenantUsers.AnyAsync(x => x.TenantId == tenantId && x.UserId == actorUserId && x.IsActive && x.TenantRole.Permissions.Any(p=>p.PermissionKey==TenantPermissions.MembersDelete), ct);
        if (!canManage) return false;
        var membership = await dbContext.TenantUsers.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == membershipId, ct);
        if (membership is null) return false;
        var wasActive = membership.IsActive;
        membership.Deactivate();
        audit.Record(actorUserId, AuditActions.MemberDeactivated, "TenantMembership", membershipId.ToString(), correlationId, tenantId,
            new { membershipId, wasActive });
        await dbContext.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> AssignRoleAsync(Guid tenantId, Guid actorUserId, Guid membershipId, Guid roleId, string correlationId, CancellationToken ct)
    {
        var membership = await dbContext.TenantUsers.SingleOrDefaultAsync(x => x.Id == membershipId && x.TenantId == tenantId, ct);
        var role = await dbContext.TenantRoles.Include(x => x.Permissions)
            .SingleOrDefaultAsync(x => x.Id == roleId && x.TenantId == tenantId, ct);
        if (membership is null || role is null) return false;
        // No privilege escalation: the actor can only move a member onto a role whose permissions
        // are a subset of the actor's own (architectural decision 2026-09-02, no tenant.manage
        // exception). Behind the endpoint's tenant.members.update filter.
        var actorPermissions = await GetPermissionsAsync(tenantId, actorUserId, ct);
        if (!RoleGrant.IsWithinActorAuthority(role.Permissions.Select(x => x.PermissionKey), actorPermissions))
            throw new TenantForbiddenException("You cannot assign a role that grants permissions you do not hold.");
        var oldRoleId = membership.TenantRoleId;
        if (oldRoleId == role.Id) return true; // no-op, nothing to audit
        membership.AssignRole(role.Id);
        audit.Record(actorUserId, AuditActions.MemberRoleChanged, "TenantMembership", membershipId.ToString(), correlationId, tenantId,
            new { membershipId, oldRoleId, newRoleId = role.Id });
        await dbContext.SaveChangesAsync(ct);
        return true;
    }

    private static string NormalizeSlug(string slug)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        if (normalized.Length is < 3 or > 100 || !SlugPattern().IsMatch(normalized))
            throw new TenantValidationException("Tenant slug is invalid.");
        return normalized;
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();
}
