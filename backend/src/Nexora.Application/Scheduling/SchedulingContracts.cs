using Nexora.Domain.Scheduling;

namespace Nexora.Application.Scheduling;

public sealed record WorkingHoursInput(Guid ProfessionalId,DayOfWeek DayOfWeek,TimeOnly StartLocal,TimeOnly EndLocal);
public sealed record WorkingHoursDto(Guid Id,Guid ProfessionalId,DayOfWeek DayOfWeek,TimeOnly StartLocal,TimeOnly EndLocal);
public sealed record BlockedPeriodInput(Guid ProfessionalId,string StartAt,string EndAt,string? Reason);
public sealed record BlockedPeriodDto(Guid Id,Guid ProfessionalId,DateTimeOffset StartAt,DateTimeOffset EndAt,string StartAtLocal,string EndAtLocal,string? Reason);
public sealed record AppointmentInput(Guid CustomerId,Guid ProfessionalId,Guid ServiceId,string StartAt,string? Notes);
public sealed record AppointmentDto(Guid Id,Guid CustomerId,Guid ProfessionalId,Guid ServiceId,DateTimeOffset StartAt,DateTimeOffset EndAt,string StartAtLocal,string EndAtLocal,AppointmentStatus Status,string? Notes);
public sealed record AppointmentStatusInput(AppointmentStatus Status);

public interface ITenantTimeZoneProvider{Task<string> GetTimeZoneIdAsync(Guid tenantId,CancellationToken cancellationToken);}
public interface ITimeZoneService
{
    DateTimeOffset ParseTenantInstant(string value,string timeZoneId);
    DateTimeOffset LocalToUtc(DateTime local,TimeSpan offset,string timeZoneId);
    DateTimeOffset UtcToTenant(DateTimeOffset instant,string timeZoneId);
}
public interface ISchedulingService
{
    Task<IReadOnlyCollection<WorkingHoursDto>> GetWorkingHoursAsync(Guid tenantId,Guid? professionalId,CancellationToken ct);
    Task<WorkingHoursDto> SetWorkingHoursAsync(Guid tenantId,WorkingHoursInput input,CancellationToken ct);
    Task<IReadOnlyCollection<BlockedPeriodDto>> GetBlockedPeriodsAsync(Guid tenantId,DateTimeOffset startsBefore,DateTimeOffset endsAfter,CancellationToken ct);
    Task<BlockedPeriodDto> CreateBlockedPeriodAsync(Guid tenantId,BlockedPeriodInput input,CancellationToken ct);
    Task<bool> DeleteBlockedPeriodAsync(Guid tenantId,Guid id,CancellationToken ct);
    Task<IReadOnlyCollection<AppointmentDto>> GetAppointmentsAsync(Guid tenantId,DateTimeOffset startsBefore,DateTimeOffset endsAfter,CancellationToken ct);
    Task<AppointmentDto> CreateAppointmentAsync(Guid tenantId,AppointmentInput input,CancellationToken ct);
    Task<AppointmentDto?> UpdateAppointmentAsync(Guid tenantId,Guid id,AppointmentInput input,CancellationToken ct);
    Task<AppointmentDto?> ChangeStatusAsync(Guid tenantId,Guid id,AppointmentStatus status,CancellationToken ct);
}
public sealed class SchedulingValidationException(string message):Exception(message);
public sealed class SchedulingConflictException(string message):Exception(message);
