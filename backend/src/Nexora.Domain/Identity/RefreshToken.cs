namespace Nexora.Domain.Identity;
using Nexora.Domain.Tenancy;

public sealed class RefreshToken
{
    private RefreshToken() { }

    public RefreshToken(Guid userId, string tokenHash, Guid familyId, DateTimeOffset createdAt, DateTimeOffset expiresAt, Guid? tenantId = null)
    {
        Id = Guid.NewGuid(); UserId = userId; TokenHash = tokenHash; FamilyId = familyId;
        CreatedAt = createdAt; ExpiresAt = expiresAt; TenantId = tenantId;
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public string TokenHash { get; private set; } = string.Empty;
    public Guid FamilyId { get; private set; }
    public Guid? TenantId { get; private set; }
    public Tenant? Tenant { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }
    public bool IsRevoked => RevokedAt is not null;
    public bool IsExpired(DateTimeOffset now) => ExpiresAt <= now;

    public void Revoke(DateTimeOffset revokedAt, string? replacedByTokenHash = null)
    { RevokedAt = revokedAt; ReplacedByTokenHash = replacedByTokenHash; }
}
