using Nexora.Application.Tenancy;

namespace Nexora.Infrastructure.Tenancy;

public sealed class TenantContext : ITenantContext, ITenantContextInitializer
{
    private Guid? tenantId;
    private Guid? userId;
    public bool IsAvailable => tenantId.HasValue && userId.HasValue;
    public Guid TenantId => tenantId ?? throw new InvalidOperationException("Tenant context is not available.");
    public Guid UserId => userId ?? throw new InvalidOperationException("Tenant context is not available.");
    public void Initialize(Guid tenant, Guid user)
    {
        if (IsAvailable) throw new InvalidOperationException("Tenant context is immutable after initialization.");
        tenantId = tenant; userId = user;
    }
}
