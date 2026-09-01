using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nexora.Application.Verticals;
using Nexora.Domain.Catalog;
using Nexora.Infrastructure.Persistence;

namespace Nexora.Infrastructure.Verticals;

public sealed class VerticalSetupService(NexoraDbContext db, IOptions<VerticalSetupOptions> options) : IVerticalSetupService
{
    public async Task<VerticalSetupView?> GetAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var segmentCode = await db.Tenants.Where(x => x.Id == tenantId)
            .Join(db.BusinessSegments, tenant => tenant.SegmentId, segment => segment.Id, (_, segment) => segment.Code)
            .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        return segmentCode is not null && options.Value.Templates.TryGetValue(segmentCode, out var template)
            ? Map(segmentCode, template) : null;
    }

    public async Task<AppliedVerticalSetup> ApplyAsync(Guid tenantId, ApplyVerticalSetup input, CancellationToken cancellationToken)
    {
        var template = await GetAsync(tenantId, cancellationToken)
            ?? throw new VerticalSetupValidationException("No vertical template is configured for this tenant segment.");
        if (input.Services.Count != input.Services.Select(x => x.PresetCode).Distinct(StringComparer.OrdinalIgnoreCase).Count())
            throw new VerticalSetupValidationException("A service preset can only be selected once.");
        var presets = template.ServicePresets.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
        var created = new List<Service>();
        foreach (var selection in input.Services)
        {
            if (!presets.TryGetValue(selection.PresetCode, out var preset) || selection.DurationMinutes <= 0 || selection.Price < 0)
                throw new VerticalSetupValidationException("Preset, duration or price is invalid.");
            if (await db.Services.AnyAsync(x => x.TenantId == tenantId && x.Name == preset.Name, cancellationToken)) continue;
            var service = new Service(tenantId, preset.Name, preset.Category, selection.DurationMinutes, selection.Price);
            db.Services.Add(service); created.Add(service);
        }
        await db.SaveChangesAsync(cancellationToken);
        return new(created.Select(x => x.Id).ToArray());
    }

    private static VerticalSetupView Map(string code, VerticalTemplateOptions x) => new(code, x.Name, x.OnboardingTitle,
        x.OnboardingDescription, x.DashboardTitle, new(x.Terminology.Customer, x.Terminology.Professional,
            x.Terminology.Service, x.Terminology.Schedule), x.Services.Select(s => new VerticalServicePreset(s.Code, s.Name, s.Category)).ToArray());
}
