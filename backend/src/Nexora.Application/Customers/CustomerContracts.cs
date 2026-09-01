namespace Nexora.Application.Customers;
public sealed record CustomerInput(string Name,string? Phone,string? Email,DateOnly? BirthDate,string? Notes);
public sealed record CustomerView(Guid Id,string Name,string? Phone,string? Email,DateOnly? BirthDate,string? Notes,string Status,DateTimeOffset CreatedAt,DateTimeOffset UpdatedAt);
public sealed record CustomerPage(IReadOnlyList<CustomerView> Items,int Page,int PageSize,int Total);
public interface ICustomerService{Task<CustomerPage> SearchAsync(Guid tenantId,string? search,string? status,string sort,int page,int pageSize,CancellationToken ct);Task<CustomerView?> GetAsync(Guid tenantId,Guid id,CancellationToken ct);Task<CustomerView>CreateAsync(Guid tenantId,CustomerInput input,CancellationToken ct);Task<CustomerView?>UpdateAsync(Guid tenantId,Guid id,CustomerInput input,CancellationToken ct);Task<bool>DeleteAsync(Guid tenantId,Guid id,CancellationToken ct);}
public sealed class CustomerValidationException(string message):Exception(message);
