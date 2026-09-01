using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Nexora.Application.Scheduling;
using Nexora.Infrastructure.Persistence;

namespace Nexora.Infrastructure.Scheduling;

public sealed class TenantTimeZoneProvider(NexoraDbContext db):ITenantTimeZoneProvider
{
    public async Task<string> GetTimeZoneIdAsync(Guid tenantId,CancellationToken ct)=>await db.Tenants.Where(x=>x.Id==tenantId&&x.IsActive).Select(x=>x.TimeZoneId).SingleOrDefaultAsync(ct)??throw new SchedulingValidationException("Tenant timezone is unavailable.");
}

public sealed partial class TimeZoneService:ITimeZoneService
{
    public DateTimeOffset ParseTenantInstant(string value,string timeZoneId)
    {
        if(string.IsNullOrWhiteSpace(value)||!ExplicitOffset().IsMatch(value)||!DateTimeOffset.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out var instant))throw new SchedulingValidationException("Date and time must use ISO 8601 with an explicit offset.");
        var local=DateTime.SpecifyKind(instant.DateTime,DateTimeKind.Unspecified);
        return LocalToUtc(local,instant.Offset,timeZoneId);
    }
    public DateTimeOffset LocalToUtc(DateTime local,TimeSpan offset,string timeZoneId)
    {
        var zone=Find(timeZoneId);local=DateTime.SpecifyKind(local,DateTimeKind.Unspecified);
        if(zone.IsInvalidTime(local))throw new SchedulingValidationException("The local time does not exist in the tenant timezone due to a daylight-saving transition.");
        var offsets=zone.IsAmbiguousTime(local)?zone.GetAmbiguousTimeOffsets(local):[zone.GetUtcOffset(local)];
        if(!offsets.Contains(offset))throw new SchedulingValidationException(zone.IsAmbiguousTime(local)?"The ambiguous local time requires one of the valid explicit offsets.":"The supplied offset does not match the tenant timezone.");
        return new DateTimeOffset(local,offset).ToUniversalTime();
    }
    public DateTimeOffset UtcToTenant(DateTimeOffset instant,string timeZoneId)=>TimeZoneInfo.ConvertTime(instant.ToUniversalTime(),Find(timeZoneId));
    private static TimeZoneInfo Find(string id){if(!TimeZoneInfo.TryFindSystemTimeZoneById(id,out var zone)||!zone.HasIanaId)throw new SchedulingValidationException("Tenant timezone is invalid.");return zone;}
    [GeneratedRegex(@"(Z|[+-]\d{2}:\d{2})$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)]private static partial Regex ExplicitOffset();
}
