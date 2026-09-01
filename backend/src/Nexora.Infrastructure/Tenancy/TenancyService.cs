using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Nexora.Application.Tenancy;
using Nexora.Domain.Tenancy;
using Nexora.Infrastructure.Persistence;

namespace Nexora.Infrastructure.Tenancy;

public sealed partial class TenancyService(NexoraDbContext dbContext, TimeProvider timeProvider) : ITenancyService
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

    public Task<Guid?> ResolveMembershipTenantAsync(Guid userId, string slug, CancellationToken ct)
    {
        var normalized = NormalizeSlug(slug);
        return dbContext.TenantUsers.Where(x => x.UserId == userId && x.IsActive && x.Tenant.IsActive && x.Tenant.Slug == normalized)
            .Select(x => (Guid?)x.TenantId).SingleOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyCollection<TenantMembership>> GetMembersAsync(Guid tenantId, CancellationToken ct) =>
        await dbContext.TenantUsers.Where(x => x.TenantId == tenantId)
            .Select(x => new TenantMembership(x.Id, x.UserId, x.User.Email, x.TenantRole.Name, x.IsActive)).ToArrayAsync(ct);

    public async Task<bool> DeactivateMembershipAsync(Guid tenantId, Guid actorUserId, Guid membershipId, CancellationToken ct)
    {
        var canManage = await dbContext.TenantUsers.AnyAsync(x => x.TenantId == tenantId && x.UserId == actorUserId && x.IsActive && x.TenantRole.Permissions.Any(p=>p.PermissionKey==TenantPermissions.CustomersDelete), ct);
        if (!canManage) return false;
        var membership = await dbContext.TenantUsers.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == membershipId, ct);
        if (membership is null) return false;
        membership.Deactivate(); await dbContext.SaveChangesAsync(ct); return true;
    }
    public async Task<bool> AssignRoleAsync(Guid tenantId,Guid membershipId,Guid roleId,CancellationToken ct){var membership=await dbContext.TenantUsers.SingleOrDefaultAsync(x=>x.Id==membershipId&&x.TenantId==tenantId,ct);var role=await dbContext.TenantRoles.SingleOrDefaultAsync(x=>x.Id==roleId&&x.TenantId==tenantId,ct);if(membership is null||role is null)return false;membership.AssignRole(role.Id);await dbContext.SaveChangesAsync(ct);return true;}

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
