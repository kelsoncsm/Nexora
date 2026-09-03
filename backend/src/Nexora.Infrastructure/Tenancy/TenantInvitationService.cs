using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nexora.Application.Administration;
using Nexora.Application.Identity;
using Nexora.Application.Notifications;
using Nexora.Application.Tenancy;
using Nexora.Domain.Tenancy;
using Nexora.Infrastructure.Notifications;
using Nexora.Infrastructure.Persistence;

namespace Nexora.Infrastructure.Tenancy;

/// <summary>
/// Tenant member invitation workflow. Kept apart from <see cref="TenancyService"/> so members and
/// invitations stay separate surfaces. The tenant is always the caller's session tenant — this
/// service never trusts a tenant id from the client. Raw tokens are generated once, hashed with
/// SHA-256 for storage and are single-use (a status guard makes an accepted/cancelled/expired
/// invitation reject its token).
/// </summary>
public sealed class TenantInvitationService(
    NexoraDbContext db,
    TimeProvider clock,
    IAuditLogWriter audit,
    IIdentityService identity,
    ITenancyService tenancy,
    IEmailOutbox emailOutbox,
    IOptions<TenantInvitationOptions> options,
    IOptions<EmailOptions> emailOptions) : ITenantInvitationService
{
    public async Task<IReadOnlyList<TenantInvitationView>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var rows = await db.TenantInvitations
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new InvitationRow(
                x.Id, x.Email, x.TenantRoleId, x.TenantRole.Name, x.Status,
                x.CreatedAt, x.ExpiresAt, x.AcceptedAt, x.CancelledAt, x.InvitedByUserId))
            .ToListAsync(ct);

        var inviterIds = rows.Select(r => r.InvitedByUserId).Distinct().ToArray();
        var inviters = await db.Users.Where(u => inviterIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Email, ct);

        return rows.Select(r => new TenantInvitationView(
            r.Id, r.Email, r.TenantRoleId, r.RoleName,
            EffectiveStatus(r.Status, r.ExpiresAt, now),
            inviters.GetValueOrDefault(r.InvitedByUserId, string.Empty),
            r.CreatedAt, r.ExpiresAt, r.AcceptedAt, r.CancelledAt)).ToList();
    }

    public async Task<IReadOnlyList<AssignableRole>> GetAssignableRolesAsync(Guid tenantId, Guid actorUserId, CancellationToken ct)
    {
        // Only offer roles the actor could actually assign — the same subset rule the create/resend
        // paths enforce — so the picker never shows an option the backend would 403 on.
        var actorPermissions = (await tenancy.GetPermissionsAsync(tenantId, actorUserId, ct)).ToHashSet(StringComparer.Ordinal);
        var roles = await db.TenantRoles.Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.IsSystem).ThenBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.IsSystem, Keys = x.Permissions.Select(p => p.PermissionKey).ToList() })
            .ToListAsync(ct);
        return roles
            .Where(r => RoleGrant.IsWithinActorAuthority(r.Keys, actorPermissions))
            .Select(r => new AssignableRole(r.Id, r.Name, r.IsSystem))
            .ToList();
    }

    public async Task<TenantInvitationView> CreateAsync(
        Guid tenantId, Guid actorUserId, CreateInvitationInput input, string correlationId, CancellationToken ct)
    {
        var email = (input.Email ?? string.Empty).Trim();
        if (email.Length == 0 || !email.Contains('@', StringComparison.Ordinal) || email.Length > 320)
            throw new TenantValidationException("A valid email is required.");
        var normalized = NormalizeEmail(email);
        var now = clock.GetUtcNow();

        var role = await db.TenantRoles.Include(x => x.Permissions)
            .SingleOrDefaultAsync(x => x.Id == input.RoleId && x.TenantId == tenantId, ct)
            ?? throw new TenantValidationException("The selected role does not belong to this company.");
        await EnsureActorCanGrantRoleAsync(tenantId, actorUserId, role, ct);

        if (await db.TenantUsers.AnyAsync(x => x.TenantId == tenantId && x.IsActive && x.User.NormalizedEmail == normalized, ct))
            throw new TenantConflictException("This person is already a member of the company.");

        var raw = GenerateToken();
        var tokenHash = HashToken(raw);
        var context = await LoadContextAsync(tenantId, actorUserId, ct);

        var pending = await db.TenantInvitations
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.NormalizedEmail == normalized
                && x.Status == TenantInvitationStatus.Pending, ct);

        TenantInvitation invitation;
        if (pending is not null)
        {
            if (pending.ExpiresAt > now)
                throw new TenantConflictException("There is already a pending invitation for this email.");
            // Expired but still Pending in the row — re-issue it in place (keeps the (tenant,email) slot).
            pending.Reissue(role.Id, tokenHash, now, now.AddDays(options.Value.ExpirationDays));
            invitation = pending;
        }
        else
        {
            invitation = new TenantInvitation(tenantId, email, normalized, role.Id, tokenHash,
                actorUserId, now, now.AddDays(options.Value.ExpirationDays));
            db.TenantInvitations.Add(invitation);
        }

        audit.Record(actorUserId, AuditActions.MemberInvited, "TenantInvitation", invitation.Id.ToString(),
            correlationId, tenantId, new { invitation.Id, email, roleId = role.Id });
        emailOutbox.EnqueueTenantInvitation(invitation.Id, tenantId, email,
            context.TenantName, role.Name, context.InviterEmail, BuildAcceptUrl(raw));

        await db.SaveChangesAsync(ct);
        return ToView(invitation, role.Name, context.InviterEmail, now);
    }

    public async Task<TenantInvitationView?> ResendAsync(
        Guid tenantId, Guid actorUserId, Guid invitationId, string correlationId, CancellationToken ct)
    {
        var invitation = await db.TenantInvitations
            .SingleOrDefaultAsync(x => x.Id == invitationId && x.TenantId == tenantId, ct);
        if (invitation is null) return null;
        if (invitation.Status != TenantInvitationStatus.Pending)
            throw new TenantValidationException("Only a pending invitation can be resent.");

        var roleEntity = await db.TenantRoles.Include(x => x.Permissions)
            .SingleAsync(x => x.Id == invitation.TenantRoleId, ct);
        // Re-issuing the invitation re-uses its role, so the same subset rule applies to whoever
        // resends it — a lower-privileged actor cannot revive an over-privileged invitation.
        await EnsureActorCanGrantRoleAsync(tenantId, actorUserId, roleEntity, ct);
        var role = roleEntity.Name;

        var now = clock.GetUtcNow();
        var raw = GenerateToken();
        invitation.RotateToken(HashToken(raw), now, now.AddDays(options.Value.ExpirationDays));

        var context = await LoadContextAsync(tenantId, actorUserId, ct);

        audit.Record(actorUserId, AuditActions.InvitationResent, "TenantInvitation", invitation.Id.ToString(),
            correlationId, tenantId, new { invitation.Id });
        emailOutbox.EnqueueTenantInvitation(invitation.Id, tenantId, invitation.Email,
            context.TenantName, role, context.InviterEmail, BuildAcceptUrl(raw));

        await db.SaveChangesAsync(ct);
        return ToView(invitation, role, context.InviterEmail, now);
    }

    public async Task<bool> CancelAsync(
        Guid tenantId, Guid actorUserId, Guid invitationId, string correlationId, CancellationToken ct)
    {
        var invitation = await db.TenantInvitations
            .SingleOrDefaultAsync(x => x.Id == invitationId && x.TenantId == tenantId, ct);
        if (invitation is null) return false;
        if (invitation.Status != TenantInvitationStatus.Pending)
            throw new TenantValidationException("Only a pending invitation can be cancelled.");

        invitation.Cancel(clock.GetUtcNow());
        audit.Record(actorUserId, AuditActions.InvitationCancelled, "TenantInvitation", invitation.Id.ToString(),
            correlationId, tenantId, new { invitation.Id });
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<AcceptInvitationResult> AcceptAsync(AcceptInvitationInput input, string correlationId, CancellationToken ct)
    {
        var token = (input.Token ?? string.Empty).Trim();
        if (token.Length == 0) throw new TenantValidationException("Invitation is invalid or has expired.");

        var now = clock.GetUtcNow();
        var invitation = await db.TenantInvitations.Include(x => x.Tenant)
            .SingleOrDefaultAsync(x => x.TokenHash == HashToken(token), ct);
        if (invitation is null || !invitation.IsPending(now) || !invitation.Tenant.IsActive)
            throw new TenantValidationException("Invitation is invalid or has expired.");

        var role = await db.TenantRoles
            .SingleOrDefaultAsync(x => x.Id == invitation.TenantRoleId && x.TenantId == invitation.TenantId, ct)
            ?? throw new TenantValidationException("The invitation role is no longer available. Ask for a new invitation.");

        var existing = await db.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == invitation.NormalizedEmail, ct);
        Guid userId;
        bool accountCreated;
        if (existing is not null)
        {
            // The invitee must prove control of the account for the *invited* e-mail. Someone signed
            // in as a different person cannot accept — the e-mail comes from the invitation, and the
            // password must match that account.
            userId = await identity.VerifyCredentialsAsync(invitation.NormalizedEmail, input.Password ?? string.Empty, ct)
                ?? throw new InvalidCredentialsException();
            accountCreated = false;
        }
        else
        {
            if (!string.Equals(input.Password, input.PasswordConfirmation, StringComparison.Ordinal))
                throw new TenantValidationException("Password confirmation does not match.");
            // Reuses the registration path: same password policy, hasher, e-mail normalisation and
            // default role. The identity e-mail is the invitation's, never the request body's.
            userId = await identity.ProvisionInvitedUserAsync(invitation.Email, input.Password ?? string.Empty, invitation.TenantId, ct);
            accountCreated = true;
        }

        var membership = await db.TenantUsers
            .SingleOrDefaultAsync(x => x.TenantId == invitation.TenantId && x.UserId == userId, ct);
        if (membership is null)
            db.TenantUsers.Add(new TenantUser(invitation.TenantId, userId, role.Id, now));
        else
        {
            membership.AssignRole(role.Id);
            membership.Reactivate();
        }

        invitation.Accept(userId, now);
        audit.Record(userId, AuditActions.InvitationAccepted, "TenantInvitation", invitation.Id.ToString(),
            correlationId, invitation.TenantId, new { invitation.Id, roleId = role.Id, accountCreated });

        await db.SaveChangesAsync(ct);
        return new AcceptInvitationResult(invitation.TenantId, invitation.Tenant.Slug, invitation.Tenant.Name, accountCreated);
    }

    // ---- helpers ----------------------------------------------------------------------------

    /// <summary>
    /// No privilege escalation: an actor may only invite someone to a role whose permissions are a
    /// subset of the actor's own effective permissions. No exception for <c>tenant.manage</c> or
    /// system roles (architectural decision 2026-09-02).
    /// </summary>
    private async Task EnsureActorCanGrantRoleAsync(Guid tenantId, Guid actorUserId, TenantRole role, CancellationToken ct)
    {
        var actorPermissions = await tenancy.GetPermissionsAsync(tenantId, actorUserId, ct);
        if (!RoleGrant.IsWithinActorAuthority(role.Permissions.Select(x => x.PermissionKey), actorPermissions))
            throw new TenantForbiddenException("You cannot invite someone to a role that grants permissions you do not hold.");
    }

    private async Task<InvitationContext> LoadContextAsync(Guid tenantId, Guid actorUserId, CancellationToken ct)
    {
        var tenantName = await db.Tenants.Where(x => x.Id == tenantId).Select(x => x.Name).SingleAsync(ct);
        var inviterEmail = await db.Users.Where(x => x.Id == actorUserId).Select(x => x.Email).SingleOrDefaultAsync(ct)
            ?? string.Empty;
        return new InvitationContext(tenantName, inviterEmail);
    }

    private string BuildAcceptUrl(string rawToken)
    {
        var baseUrl = emailOptions.Value.ApplicationUrl?.TrimEnd('/');
        return string.IsNullOrEmpty(baseUrl)
            ? $"convite/aceitar?token={rawToken}"
            : $"{baseUrl}/convite/aceitar?token={Uri.EscapeDataString(rawToken)}";
    }

    private static TenantInvitationView ToView(TenantInvitation x, string roleName, string inviterEmail, DateTimeOffset now) =>
        new(x.Id, x.Email, x.TenantRoleId, roleName, EffectiveStatus(x.Status, x.ExpiresAt, now),
            inviterEmail, x.CreatedAt, x.ExpiresAt, x.AcceptedAt, x.CancelledAt);

    private static string EffectiveStatus(TenantInvitationStatus status, DateTimeOffset expiresAt, DateTimeOffset now) =>
        status == TenantInvitationStatus.Pending && expiresAt <= now ? "Expired" : status.ToString();

    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private sealed record InvitationRow(
        Guid Id, string Email, Guid TenantRoleId, string RoleName, TenantInvitationStatus Status,
        DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, DateTimeOffset? AcceptedAt, DateTimeOffset? CancelledAt,
        Guid InvitedByUserId);

    private sealed record InvitationContext(string TenantName, string InviterEmail);
}
