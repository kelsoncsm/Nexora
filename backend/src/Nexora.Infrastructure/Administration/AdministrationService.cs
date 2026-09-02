using Microsoft.EntityFrameworkCore;
using Nexora.Application.Administration;
using Nexora.Domain.Administration;
using Nexora.Infrastructure.Persistence;

namespace Nexora.Infrastructure.Administration;

public sealed class AdministrationService(NexoraDbContext db, TimeProvider timeProvider) : IAdministrationService
{
    public async Task<AdminDashboard> DashboardAsync(CancellationToken ct) => new(
        await db.Tenants.CountAsync(ct), await db.Tenants.CountAsync(x => x.IsActive, ct),
        await db.Users.CountAsync(ct), await db.BusinessSegments.CountAsync(x => x.IsActive, ct));

    public async Task<IReadOnlyList<AdminTenant>> GetTenantsAsync(CancellationToken ct) =>
        await db.Tenants.OrderBy(x => x.Name).Select(x => new AdminTenant(x.Id, x.Name, x.Slug, x.IsActive, x.CreatedAt)).ToListAsync(ct);

    public Task<AdminTenant?> GetTenantAsync(Guid id, CancellationToken ct) => db.Tenants.Where(x => x.Id == id)
        .Select(x => new AdminTenant(x.Id, x.Name, x.Slug, x.IsActive, x.CreatedAt)).SingleOrDefaultAsync(ct);

    public async Task<bool> SetTenantActiveAsync(Guid actorId, Guid id, bool active, string correlationId, CancellationToken ct)
    {
        var tenant = await db.Tenants.SingleOrDefaultAsync(x => x.Id == id, ct); if (tenant is null) return false;
        if (active) tenant.Activate(); else tenant.Deactivate();
        db.AuditLogs.Add(new AuditLog(actorId, active ? "tenant.activate" : "tenant.deactivate", "Tenant", id.ToString(), true, correlationId, timeProvider.GetUtcNow()));
        await db.SaveChangesAsync(ct); return true;
    }

    public async Task<IReadOnlyList<AdminUser>> GetUsersAsync(CancellationToken ct) =>
        await db.Users.OrderBy(x => x.Email).Select(x => new AdminUser(x.Id, x.Email, x.IsActive, x.CreatedAt)).ToListAsync(ct);

    public async Task<IReadOnlyList<SegmentView>> GetSegmentsAsync(CancellationToken ct) =>
        await db.BusinessSegments.OrderBy(x => x.Code).Select(x => new SegmentView(x.Id, x.Code, x.Name, x.IsActive, x.CreatedAt)).ToListAsync(ct);

    public async Task<SegmentView> CreateSegmentAsync(Guid actorId, string code, string name, string correlationId, CancellationToken ct)
    {
        var normalizedCode = code.Trim().ToUpperInvariant(); var normalizedName = name.Trim(); Validate(normalizedCode, normalizedName);
        if (await db.BusinessSegments.AnyAsync(x => x.Code == normalizedCode, ct)) throw new AdministrationConflictException("Segment code already exists.");
        var segment = new BusinessSegment(normalizedCode, normalizedName, timeProvider.GetUtcNow()); db.BusinessSegments.Add(segment);
        db.AuditLogs.Add(new AuditLog(actorId, "segment.create", "BusinessSegment", segment.Id.ToString(), true, correlationId, timeProvider.GetUtcNow()));
        await db.SaveChangesAsync(ct); return Map(segment);
    }

    public async Task<SegmentView?> UpdateSegmentAsync(Guid actorId, Guid id, string name, bool active, string correlationId, CancellationToken ct)
    {
        var segment = await db.BusinessSegments.SingleOrDefaultAsync(x => x.Id == id, ct); if (segment is null) return null;
        var normalizedName = name.Trim(); Validate(segment.Code, normalizedName); segment.Update(normalizedName, active);
        db.AuditLogs.Add(new AuditLog(actorId, "segment.update", "BusinessSegment", id.ToString(), true, correlationId, timeProvider.GetUtcNow()));
        await db.SaveChangesAsync(ct); return Map(segment);
    }

    public async Task<IReadOnlyList<AuditView>> GetAuditAsync(CancellationToken ct) => await db.AuditLogs
        .OrderByDescending(x => x.OccurredAt).Take(200)
        .Select(x => new AuditView(x.Id, x.ActorUserId, x.TenantId, x.Action, x.TargetType, x.TargetId, x.Succeeded, x.CorrelationId, x.Details, x.OccurredAt)).ToListAsync(ct);

    private static void Validate(string code, string name)
    { if (code.Length is < 2 or > 50 || name.Length is < 2 or > 120) throw new AdministrationValidationException("Segment code or name is invalid."); }
    private static SegmentView Map(BusinessSegment x) => new(x.Id, x.Code, x.Name, x.IsActive, x.CreatedAt);
}
