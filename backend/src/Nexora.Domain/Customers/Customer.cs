namespace Nexora.Domain.Customers;
public sealed class Customer
{
 private Customer(){} public Customer(Guid tenantId,string name,string? phone,string? email,DateOnly? birthDate,string? notes,DateTimeOffset now){Id=Guid.NewGuid();TenantId=tenantId;Update(name,phone,email,birthDate,notes,now);Status="Active";CreatedAt=now;}
 public Guid Id{get;private set;}public Guid TenantId{get;private set;}public string Name{get;private set;}=string.Empty;public string? Phone{get;private set;}public string? Email{get;private set;}public DateOnly? BirthDate{get;private set;}public string? Notes{get;private set;}public string Status{get;private set;}="Active";public DateTimeOffset CreatedAt{get;private set;}public DateTimeOffset UpdatedAt{get;private set;}
 public void Update(string name,string? phone,string? email,DateOnly? birthDate,string? notes,DateTimeOffset now){Name=name;Phone=phone;Email=email;BirthDate=birthDate;Notes=notes;UpdatedAt=now;}
}
