namespace Nexora.Application.Tenancy;

/// <summary>An invitation as shown to tenant administrators. Never carries the token or its hash.</summary>
public sealed record TenantInvitationView(
    Guid Id,
    string Email,
    Guid RoleId,
    string RoleName,
    string Status,
    string InvitedByEmail,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset? CancelledAt);

/// <summary>Create-invitation payload. The tenant is taken from the caller's session, never the body.</summary>
public sealed record CreateInvitationInput(string Email, Guid RoleId);

/// <summary>
/// Accept-invitation payload. <see cref="Token"/> is the raw invitation token from the e-mail link;
/// it travels in the body (never the URL) so it does not leak into logs/tracing/proxies.
/// <see cref="PasswordConfirmation"/> is checked only when the invited e-mail has no Nexora account
/// yet (a new identity is created); for an existing account <see cref="Password"/> authenticates it.
/// The e-mail is always taken from the invitation, never the body. Nexora identities are e-mail-only,
/// so no display name is collected (same as self-registration).
/// </summary>
public sealed record AcceptInvitationInput(string Token, string Password, string? PasswordConfirmation);

/// <summary>Where the invitee should go after accepting (log in, then this company is available).</summary>
public sealed record AcceptInvitationResult(Guid TenantId, string TenantSlug, string TenantName, bool AccountCreated);

/// <summary>A role the inviter can assign, for the "invite user" role picker.</summary>
public sealed record AssignableRole(Guid Id, string Name, bool IsSystem);

public interface ITenantInvitationService
{
    Task<IReadOnlyList<TenantInvitationView>> ListAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<IReadOnlyList<AssignableRole>> GetAssignableRolesAsync(Guid tenantId, Guid actorUserId, CancellationToken cancellationToken);
    Task<TenantInvitationView> CreateAsync(Guid tenantId, Guid actorUserId, CreateInvitationInput input, string correlationId, CancellationToken cancellationToken);
    Task<TenantInvitationView?> ResendAsync(Guid tenantId, Guid actorUserId, Guid invitationId, string correlationId, CancellationToken cancellationToken);
    Task<bool> CancelAsync(Guid tenantId, Guid actorUserId, Guid invitationId, string correlationId, CancellationToken cancellationToken);
    Task<AcceptInvitationResult> AcceptAsync(AcceptInvitationInput input, string correlationId, CancellationToken cancellationToken);
}
