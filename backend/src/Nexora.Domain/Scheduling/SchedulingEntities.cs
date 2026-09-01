namespace Nexora.Domain.Scheduling;

public enum AppointmentStatus { Scheduled, Confirmed, InProgress, Completed, Cancelled, NoShow }

public sealed class WorkingHours
{
    private WorkingHours() { }
    public WorkingHours(Guid tenantId, Guid professionalId, DayOfWeek dayOfWeek, TimeOnly startLocal, TimeOnly endLocal)
    { Id=Guid.NewGuid();TenantId=tenantId;ProfessionalId=professionalId;DayOfWeek=dayOfWeek;Set(startLocal,endLocal); }
    public Guid Id{get;private set;} public Guid TenantId{get;private set;} public Guid ProfessionalId{get;private set;}
    public DayOfWeek DayOfWeek{get;private set;} public TimeOnly StartLocal{get;private set;} public TimeOnly EndLocal{get;private set;}
    public void Set(TimeOnly startLocal,TimeOnly endLocal){if(endLocal<=startLocal)throw new ArgumentException("Working hours end must be after start.");StartLocal=startLocal;EndLocal=endLocal;}
}

public sealed class BlockedPeriod
{
    private BlockedPeriod() { }
    public BlockedPeriod(Guid tenantId,Guid professionalId,DateTimeOffset startAt,DateTimeOffset endAt,string?reason)
    {Id=Guid.NewGuid();TenantId=tenantId;ProfessionalId=professionalId;Set(startAt,endAt,reason);}
    public Guid Id{get;private set;}public Guid TenantId{get;private set;}public Guid ProfessionalId{get;private set;}
    public DateTimeOffset StartAt{get;private set;}public DateTimeOffset EndAt{get;private set;}public string?Reason{get;private set;}
    public void Set(DateTimeOffset startAt,DateTimeOffset endAt,string?reason){if(endAt<=startAt)throw new ArgumentException("Blocked period end must be after start.");StartAt=startAt.ToUniversalTime();EndAt=endAt.ToUniversalTime();Reason=reason;}
}

public sealed class Appointment
{
    private Appointment() { }
    public Appointment(Guid tenantId,Guid customerId,Guid professionalId,Guid serviceId,DateTimeOffset startAt,DateTimeOffset endAt,string?notes,DateTimeOffset now)
    {Id=Guid.NewGuid();TenantId=tenantId;CustomerId=customerId;ProfessionalId=professionalId;ServiceId=serviceId;StartAt=startAt.ToUniversalTime();EndAt=endAt.ToUniversalTime();Notes=notes;Status=AppointmentStatus.Scheduled;CreatedAt=UpdatedAt=now.ToUniversalTime();}
    public Guid Id{get;private set;}public Guid TenantId{get;private set;}public Guid CustomerId{get;private set;}public Guid ProfessionalId{get;private set;}public Guid ServiceId{get;private set;}
    public DateTimeOffset StartAt{get;private set;}public DateTimeOffset EndAt{get;private set;}public AppointmentStatus Status{get;private set;}public string?Notes{get;private set;}public DateTimeOffset CreatedAt{get;private set;}public DateTimeOffset UpdatedAt{get;private set;}
    public void Reschedule(Guid customerId,Guid professionalId,Guid serviceId,DateTimeOffset startAt,DateTimeOffset endAt,string?notes,DateTimeOffset now){CustomerId=customerId;ProfessionalId=professionalId;ServiceId=serviceId;StartAt=startAt.ToUniversalTime();EndAt=endAt.ToUniversalTime();Notes=notes;UpdatedAt=now.ToUniversalTime();}
    public void ChangeStatus(AppointmentStatus status,DateTimeOffset now){Status=status;UpdatedAt=now.ToUniversalTime();}
}
