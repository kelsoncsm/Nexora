namespace Nexora.Domain.Tenancy;

public enum TenantInvitationStatus
{
    Pending,
    Accepted,
    Cancelled,
}

/// <summary>
/// A pending invitation for an e-mail address to join a tenant with a specific role. The raw token
/// is generated once, delivered only to the invitee and never persisted — only its SHA-256 hash is
/// stored (<see cref="TokenHash"/>). The token is single-use: it stops working once the invitation
/// is accepted, cancelled or expired. "Expired" is derived from <see cref="ExpiresAt"/> rather than
/// stored, so a background job is not required to keep state correct.
/// </summary>
public sealed class TenantInvitation
{
    private TenantInvitation() { }

    public TenantInvitation(
        Guid tenantId,
        string email,
        string normalizedEmail,
        Guid tenantRoleId,
        string tokenHash,
        Guid invitedByUserId,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        Email = email;
        NormalizedEmail = normalizedEmail;
        TenantRoleId = tenantRoleId;
        TokenHash = tokenHash;
        InvitedByUserId = invitedByUserId;
        CreatedAt = createdAt.ToUniversalTime();
        ExpiresAt = expiresAt.ToUniversalTime();
        Status = TenantInvitationStatus.Pending;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Tenant Tenant { get; private set; } = null!;
    public string Email { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public Guid TenantRoleId { get; private set; }
    public TenantRole TenantRole { get; private set; } = null!;
    public string TokenHash { get; private set; } = string.Empty;
    public Guid InvitedByUserId { get; private set; }
    public TenantInvitationStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public Guid? AcceptedByUserId { get; private set; }

    /// <summary>True while the invitation can still be accepted with a valid token.</summary>
    public bool IsPending(DateTimeOffset now) =>
        Status == TenantInvitationStatus.Pending && ExpiresAt > now.ToUniversalTime();

    /// <summary>The state a caller should surface: Pending collapses to Expired past <see cref="ExpiresAt"/>.</summary>
    public string EffectiveStatus(DateTimeOffset now) => Status switch
    {
        TenantInvitationStatus.Pending when ExpiresAt <= now.ToUniversalTime() => "Expired",
        _ => Status.ToString(),
    };

    /// <summary>Rotates the token (resend). The previous token hash is discarded and stops working.</summary>
    public void RotateToken(string tokenHash, DateTimeOffset now, DateTimeOffset expiresAt)
    {
        if (Status != TenantInvitationStatus.Pending)
            throw new InvalidOperationException("Only a pending invitation can be resent.");
        TokenHash = tokenHash;
        ExpiresAt = expiresAt.ToUniversalTime();
        CreatedAt = now.ToUniversalTime();
    }

    /// <summary>
    /// Re-issues an expired-but-still-Pending invitation as a fresh one, possibly with a different
    /// role. Keeps the same row so the tenant/e-mail uniqueness holds.
    /// </summary>
    public void Reissue(Guid tenantRoleId, string tokenHash, DateTimeOffset now, DateTimeOffset expiresAt)
    {
        if (Status != TenantInvitationStatus.Pending)
            throw new InvalidOperationException("Only a pending invitation can be re-issued.");
        TenantRoleId = tenantRoleId;
        TokenHash = tokenHash;
        CreatedAt = now.ToUniversalTime();
        ExpiresAt = expiresAt.ToUniversalTime();
    }

    public void Accept(Guid acceptedByUserId, DateTimeOffset now)
    {
        if (Status != TenantInvitationStatus.Pending)
            throw new InvalidOperationException("Only a pending invitation can be accepted.");
        Status = TenantInvitationStatus.Accepted;
        AcceptedAt = now.ToUniversalTime();
        AcceptedByUserId = acceptedByUserId;
        // The token hash is kept for audit correlation but is now inert (status guard above).
    }

    public void Cancel(DateTimeOffset now)
    {
        if (Status != TenantInvitationStatus.Pending)
            throw new InvalidOperationException("Only a pending invitation can be cancelled.");
        Status = TenantInvitationStatus.Cancelled;
        CancelledAt = now.ToUniversalTime();
    }
}
