using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Globalization;
using Nexora.Application.Scheduling;
using Nexora.Domain.Scheduling;
using Nexora.Infrastructure.Persistence;

namespace Nexora.Infrastructure.Scheduling;

public sealed class SchedulingService(NexoraDbContext db,ITenantTimeZoneProvider zones,ITimeZoneService timeZones,TimeProvider clock):ISchedulingService
{
    public async Task<IReadOnlyCollection<WorkingHoursDto>> GetWorkingHoursAsync(Guid tenantId,Guid? professionalId,CancellationToken ct)=>await db.WorkingHours.Where(x=>x.TenantId==tenantId&&(!professionalId.HasValue||x.ProfessionalId==professionalId)).OrderBy(x=>x.DayOfWeek).Select(x=>new WorkingHoursDto(x.Id,x.ProfessionalId,x.DayOfWeek,x.StartLocal,x.EndLocal)).ToArrayAsync(ct);
    public async Task<WorkingHoursDto> SetWorkingHoursAsync(Guid tenantId,WorkingHoursInput input,CancellationToken ct)
    {
        if(!await db.Professionals.AnyAsync(x=>x.TenantId==tenantId&&x.Id==input.ProfessionalId&&x.IsActive,ct))throw new SchedulingValidationException("Professional is unavailable.");
        if(input.EndLocal<=input.StartLocal)throw new SchedulingValidationException("Working hours end must be after start.");
        var value=await db.WorkingHours.SingleOrDefaultAsync(x=>x.TenantId==tenantId&&x.ProfessionalId==input.ProfessionalId&&x.DayOfWeek==input.DayOfWeek,ct);
        if(value is null){value=new WorkingHours(tenantId,input.ProfessionalId,input.DayOfWeek,input.StartLocal,input.EndLocal);db.Add(value);}else value.Set(input.StartLocal,input.EndLocal);
        await db.SaveChangesAsync(ct);return new(value.Id,value.ProfessionalId,value.DayOfWeek,value.StartLocal,value.EndLocal);
    }
    public async Task<IReadOnlyCollection<BlockedPeriodDto>> GetBlockedPeriodsAsync(Guid tenantId,DateTimeOffset from,DateTimeOffset to,CancellationToken ct)
    {var zone=await zones.GetTimeZoneIdAsync(tenantId,ct);var rows=await db.BlockedPeriods.Where(x=>x.TenantId==tenantId&&x.StartAt<to.ToUniversalTime()&&x.EndAt>from.ToUniversalTime()).OrderBy(x=>x.StartAt).ToArrayAsync(ct);return rows.Select(x=>Map(x,zone)).ToArray();}
    public async Task<BlockedPeriodDto> CreateBlockedPeriodAsync(Guid tenantId,BlockedPeriodInput input,CancellationToken ct)
    {if(!await db.Professionals.AnyAsync(x=>x.TenantId==tenantId&&x.Id==input.ProfessionalId&&x.IsActive,ct))throw new SchedulingValidationException("Professional is unavailable.");var zone=await zones.GetTimeZoneIdAsync(tenantId,ct);var start=timeZones.ParseTenantInstant(input.StartAt,zone);var end=timeZones.ParseTenantInstant(input.EndAt,zone);if(end<=start)throw new SchedulingValidationException("Blocked period end must be after start.");var value=new BlockedPeriod(tenantId,input.ProfessionalId,start,end,Clean(input.Reason,500));db.Add(value);await db.SaveChangesAsync(ct);return Map(value,zone);}
    public async Task<bool> DeleteBlockedPeriodAsync(Guid tenantId,Guid id,CancellationToken ct){var row=await db.BlockedPeriods.SingleOrDefaultAsync(x=>x.TenantId==tenantId&&x.Id==id,ct);if(row is null)return false;db.Remove(row);await db.SaveChangesAsync(ct);return true;}
    public async Task<IReadOnlyCollection<AppointmentDto>> GetAppointmentsAsync(Guid tenantId,DateTimeOffset from,DateTimeOffset to,CancellationToken ct)
    {var zone=await zones.GetTimeZoneIdAsync(tenantId,ct);var rows=await db.Appointments.Where(x=>x.TenantId==tenantId&&x.StartAt<to.ToUniversalTime()&&x.EndAt>from.ToUniversalTime()).OrderBy(x=>x.StartAt).ToArrayAsync(ct);return rows.Select(x=>Map(x,zone)).ToArray();}
    public async Task<AppointmentDto> CreateAppointmentAsync(Guid tenantId,AppointmentInput input,CancellationToken ct)
    {await using var tx=await BeginTransactionAsync(ct);await LockAsync(tenantId,[input.ProfessionalId],ct);var value=await BuildAsync(tenantId,null,input,ct);db.Add(value);await db.SaveChangesAsync(ct);await CommitAsync(tx,ct);return Map(value,await zones.GetTimeZoneIdAsync(tenantId,ct));}
    public async Task<AppointmentDto?> UpdateAppointmentAsync(Guid tenantId,Guid id,AppointmentInput input,CancellationToken ct)
    {await using var tx=await BeginTransactionAsync(ct);var current=await db.Appointments.SingleOrDefaultAsync(x=>x.TenantId==tenantId&&x.Id==id,ct);if(current is null)return null;await LockAsync(tenantId,[current.ProfessionalId,input.ProfessionalId],ct);if(tx is not null)await db.Entry(current).ReloadAsync(ct);if(current.Status is AppointmentStatus.Completed or AppointmentStatus.Cancelled or AppointmentStatus.NoShow)throw new SchedulingValidationException("This appointment can no longer be rescheduled.");var candidate=await BuildAsync(tenantId,id,input,ct);current.Reschedule(input.CustomerId,input.ProfessionalId,input.ServiceId,candidate.StartAt,candidate.EndAt,candidate.Notes,clock.GetUtcNow());await db.SaveChangesAsync(ct);await CommitAsync(tx,ct);return Map(current,await zones.GetTimeZoneIdAsync(tenantId,ct));}
    public async Task<AppointmentDto?> ChangeStatusAsync(Guid tenantId,Guid id,AppointmentStatus status,CancellationToken ct)
    {await using var tx=await BeginTransactionAsync(ct);var row=await db.Appointments.SingleOrDefaultAsync(x=>x.TenantId==tenantId&&x.Id==id,ct);if(row is null)return null;if(status==AppointmentStatus.Cancelled){await LockAsync(tenantId,[row.ProfessionalId],ct);if(tx is not null)await db.Entry(row).ReloadAsync(ct);}if(!Allowed(row.Status,status))throw new SchedulingValidationException("Invalid appointment status transition.");row.ChangeStatus(status,clock.GetUtcNow());await db.SaveChangesAsync(ct);await CommitAsync(tx,ct);return Map(row,await zones.GetTimeZoneIdAsync(tenantId,ct));}
    private async Task<Appointment> BuildAsync(Guid tenantId,Guid? excludedId,AppointmentInput input,CancellationToken ct)
    {
        var valid=await db.ProfessionalServices.AnyAsync(x=>x.TenantId==tenantId&&x.ProfessionalId==input.ProfessionalId&&x.ServiceId==input.ServiceId&&x.Professional.IsActive&&x.Service.IsActive,ct);
        var customer=await db.Customers.AnyAsync(x=>x.TenantId==tenantId&&x.Id==input.CustomerId&&x.Status=="Active",ct);
        if(!valid||!customer)throw new SchedulingValidationException("Customer, professional, or service is unavailable for this tenant.");
        var duration=await db.Services.Where(x=>x.TenantId==tenantId&&x.Id==input.ServiceId).Select(x=>x.DurationMinutes).SingleAsync(ct);var zone=await zones.GetTimeZoneIdAsync(tenantId,ct);var start=timeZones.ParseTenantInstant(input.StartAt,zone);var end=start.AddMinutes(duration);var localStart=timeZones.UtcToTenant(start,zone);var localEnd=timeZones.UtcToTenant(end,zone);
        if(localStart.Date!=localEnd.Date)throw new SchedulingValidationException("Appointment must fit within one local calendar day.");
        var hours=await db.WorkingHours.AnyAsync(x=>x.TenantId==tenantId&&x.ProfessionalId==input.ProfessionalId&&x.DayOfWeek==localStart.DayOfWeek&&x.StartLocal<=TimeOnly.FromDateTime(localStart.DateTime)&&x.EndLocal>=TimeOnly.FromDateTime(localEnd.DateTime),ct);
        if(!hours)throw new SchedulingConflictException("Professional is outside configured working hours.");
        if(await db.BlockedPeriods.AnyAsync(x=>x.TenantId==tenantId&&x.ProfessionalId==input.ProfessionalId&&x.StartAt<end&&x.EndAt>start,ct))throw new SchedulingConflictException("Professional has a blocked period.");
        if(await db.Appointments.AnyAsync(x=>x.TenantId==tenantId&&x.ProfessionalId==input.ProfessionalId&&x.Id!=excludedId&&x.Status!=AppointmentStatus.Cancelled&&x.StartAt<end&&x.EndAt>start,ct))throw new SchedulingConflictException("Professional already has a conflicting appointment.");
        return new Appointment(tenantId,input.CustomerId,input.ProfessionalId,input.ServiceId,start,end,Clean(input.Notes,4000),clock.GetUtcNow());
    }
    private AppointmentDto Map(Appointment x,string zone){var start=timeZones.UtcToTenant(x.StartAt,zone);var end=timeZones.UtcToTenant(x.EndAt,zone);return new(x.Id,x.CustomerId,x.ProfessionalId,x.ServiceId,x.StartAt,x.EndAt,start.ToString("yyyy-MM-dd'T'HH:mm:sszzz",CultureInfo.InvariantCulture),end.ToString("yyyy-MM-dd'T'HH:mm:sszzz",CultureInfo.InvariantCulture),x.Status,x.Notes);}
    private BlockedPeriodDto Map(BlockedPeriod x,string zone){var start=timeZones.UtcToTenant(x.StartAt,zone);var end=timeZones.UtcToTenant(x.EndAt,zone);return new(x.Id,x.ProfessionalId,x.StartAt,x.EndAt,start.ToString("yyyy-MM-dd'T'HH:mm:sszzz",CultureInfo.InvariantCulture),end.ToString("yyyy-MM-dd'T'HH:mm:sszzz",CultureInfo.InvariantCulture),x.Reason);}
    private static bool Allowed(AppointmentStatus from,AppointmentStatus to)=>to==AppointmentStatus.Cancelled||(from,to) switch{(AppointmentStatus.Scheduled,AppointmentStatus.Confirmed)=>true,(AppointmentStatus.Confirmed,AppointmentStatus.InProgress)=>true,(AppointmentStatus.InProgress,AppointmentStatus.Completed)=>true,(_,AppointmentStatus.NoShow)=>from is AppointmentStatus.Scheduled or AppointmentStatus.Confirmed,_=>false};
    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken ct)=>PostgresAdvisoryLock.IsSupported(db)?await db.Database.BeginTransactionAsync(ct):null;
    private async Task LockAsync(Guid tenantId,IEnumerable<Guid> professionals,CancellationToken ct){if(!PostgresAdvisoryLock.IsSupported(db))return;foreach(var professionalId in professionals.Distinct().Order())await PostgresAdvisoryLock.AcquireAsync(db,"schedule",tenantId,professionalId,ct);}
    private static Task CommitAsync(IDbContextTransaction? transaction,CancellationToken ct)=>transaction is null?Task.CompletedTask:transaction.CommitAsync(ct);
    private static string? Clean(string? value,int max){var clean=value?.Trim();if(clean?.Length>max)throw new SchedulingValidationException($"Text cannot exceed {max} characters.");return string.IsNullOrEmpty(clean)?null:clean;}
}
