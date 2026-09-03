using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Nexora.Application.Identity;
using Nexora.Application.Tenancy;
using Nexora.Domain.Identity;
using Nexora.Infrastructure.Persistence;
using Nexora.Application.Notifications;

namespace Nexora.Infrastructure.Identity;

public sealed class IdentityService(
    NexoraDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    IAccessTokenGenerator tokenGenerator,
    IEmailOutbox emailOutbox,
    IOptions<IdentityOptions> options,
    TimeProvider timeProvider,
    ITenancyService tenancy) : IIdentityService
{
    private const string DefaultRole = "User";
    private const string ProfilePermission = "identity.profile";

    public async Task<AuthenticatedSession> RegisterAsync(RegisterCommand command, CancellationToken cancellationToken)
    {
        IdentityValidation.ValidateCredentials(command.Email, command.Password);
        var user = await AddNewUserAsync(command.Email, command.Password, null, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await CreateSessionAsync(user, Guid.NewGuid(), cancellationToken);
    }

    public async Task<Guid> ProvisionInvitedUserAsync(string email, string password, Guid tenantId, CancellationToken cancellationToken)
    {
        IdentityValidation.ValidatePassword(password);
        var user = await AddNewUserAsync(email, password, tenantId, cancellationToken);
        // Deliberately no SaveChanges: the invitation service commits the user, the membership and
        // the accepted invitation in one unit of work.
        return user.Id;
    }

    public async Task<Guid?> VerifyCredentialsAsync(string normalizedEmail, string password, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);
        if (user is null || !user.IsActive) return null;
        return passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password) == PasswordVerificationResult.Failed
            ? null
            : user.Id;
    }

    /// <summary>Registration side effects without the session: create the user, attach the default
    /// role (creating it and its <c>identity.profile</c> permission on first use) and enqueue the
    /// welcome e-mail. Does not call SaveChanges.</summary>
    private async Task<User> AddNewUserAsync(string email, string password, Guid? tenantId, CancellationToken cancellationToken)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (await dbContext.Users.AnyAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken))
            throw new IdentityConflictException("An account with this email already exists.");

        var now = timeProvider.GetUtcNow();
        var user = new User(email.Trim(), normalizedEmail, now);
        user.SetPasswordHash(passwordHasher.HashPassword(user, password));

        var role = await dbContext.Roles.Include(x => x.Permissions).ThenInclude(x => x.Permission)
            .SingleOrDefaultAsync(x => x.Name == DefaultRole, cancellationToken);
        if (role is null)
        {
            role = new Role(DefaultRole);
            var permission = await dbContext.Permissions.SingleOrDefaultAsync(x => x.Name == ProfilePermission, cancellationToken);
            if (permission is null)
            {
                permission = new Permission(ProfilePermission);
                dbContext.Permissions.Add(permission);
            }
            role.Permissions.Add(new RolePermission(role.Id, permission.Id));
            dbContext.Roles.Add(role);
        }
        user.Roles.Add(new UserRole(user.Id, role.Id));
        dbContext.Users.Add(user);
        emailOutbox.EnqueueWelcome(user.Id, tenantId, user.Email, user.Email.Split('@')[0]);
        return user;
    }

    public async Task<AuthenticatedSession> LoginAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var user = await LoadUserAsync(NormalizeEmail(command.Email), cancellationToken);
        if (user is null || !user.IsActive ||
            passwordHasher.VerifyHashedPassword(user, user.PasswordHash, command.Password) == PasswordVerificationResult.Failed)
            throw new InvalidCredentialsException();
        return await CreateSessionAsync(user, Guid.NewGuid(), cancellationToken);
    }

    public async Task<AuthenticatedSession> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (dbContext.Database.IsNpgsql())
            return await RefreshAtomicallyAsync(refreshToken, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var hash = Hash(refreshToken);
        var stored = await dbContext.RefreshTokens.Include(x => x.User).ThenInclude(x => x.Roles)
            .ThenInclude(x => x.Role).ThenInclude(x => x.Permissions).ThenInclude(x => x.Permission)
            .SingleOrDefaultAsync(x => x.TokenHash == hash, cancellationToken)
            ?? throw new InvalidRefreshTokenException();
        if (stored.IsRevoked)
        {
            var family = await dbContext.RefreshTokens.Where(x => x.FamilyId == stored.FamilyId && x.RevokedAt == null).ToListAsync(cancellationToken);
            foreach (var token in family) token.Revoke(now);
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new InvalidRefreshTokenException();
        }
        if (stored.IsExpired(now) || !stored.User.IsActive) throw new InvalidRefreshTokenException();

        var raw = GenerateRefreshToken();
        var replacementHash = Hash(raw);
        stored.Revoke(now, replacementHash);
        dbContext.RefreshTokens.Add(new RefreshToken(stored.UserId, replacementHash, stored.FamilyId, now,
            now.AddDays(options.Value.RefreshTokenDays), stored.TenantId));
        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildSession(stored.User, raw, stored.TenantId, cancellationToken);
    }

    private async Task<AuthenticatedSession> RefreshAtomicallyAsync(string refreshToken,CancellationToken cancellationToken)
    {
        var requestStartedAt=timeProvider.GetUtcNow();var hash=Hash(refreshToken);var raw=GenerateRefreshToken();var replacementHash=Hash(raw);
        await using var transaction=await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var identity=await dbContext.RefreshTokens.Where(x=>x.TokenHash==hash).Select(x=>new{x.Id,x.UserId,x.FamilyId,x.TenantId,x.RevokedAt}).SingleOrDefaultAsync(cancellationToken)??throw new InvalidRefreshTokenException();
        var affected=await dbContext.RefreshTokens.Where(x=>x.Id==identity.Id&&x.RevokedAt==null&&x.ExpiresAt>requestStartedAt)
            .ExecuteUpdateAsync(setters=>setters.SetProperty(x=>x.RevokedAt,requestStartedAt).SetProperty(x=>x.ReplacedByTokenHash,replacementHash),cancellationToken);
        if(affected!=1)
        {
            await transaction.RollbackAsync(cancellationToken);
            if(identity.RevokedAt.HasValue&&identity.RevokedAt.Value<requestStartedAt)
            {
                var now=timeProvider.GetUtcNow();var family=await dbContext.RefreshTokens.Where(x=>x.FamilyId==identity.FamilyId&&x.RevokedAt==null).ToListAsync(cancellationToken);
                foreach(var token in family)token.Revoke(now);await dbContext.SaveChangesAsync(cancellationToken);
            }
            throw new InvalidRefreshTokenException();
        }
        var user=await dbContext.Users.Include(x=>x.Roles).ThenInclude(x=>x.Role).ThenInclude(x=>x.Permissions).ThenInclude(x=>x.Permission).SingleOrDefaultAsync(x=>x.Id==identity.UserId&&x.IsActive,cancellationToken);
        if(user is null){await transaction.RollbackAsync(cancellationToken);throw new InvalidRefreshTokenException();}
        dbContext.RefreshTokens.Add(new RefreshToken(identity.UserId,replacementHash,identity.FamilyId,requestStartedAt,requestStartedAt.AddDays(options.Value.RefreshTokenDays),identity.TenantId));
        await dbContext.SaveChangesAsync(cancellationToken);await transaction.CommitAsync(cancellationToken);return await BuildSession(user,raw,identity.TenantId,cancellationToken);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var stored = await dbContext.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == Hash(refreshToken), cancellationToken);
        if (stored is not null && !stored.IsRevoked) { stored.Revoke(timeProvider.GetUtcNow()); await dbContext.SaveChangesAsync(cancellationToken); }
    }

    public async Task<AuthenticatedSession> SelectTenantAsync(Guid userId, string tenantSlug, string refreshToken, CancellationToken cancellationToken)
    {
        var hash = Hash(refreshToken); var now = timeProvider.GetUtcNow();
        var stored = await dbContext.RefreshTokens.Include(x => x.User).ThenInclude(x => x.Roles)
            .ThenInclude(x => x.Role).ThenInclude(x => x.Permissions).ThenInclude(x => x.Permission)
            .SingleOrDefaultAsync(x => x.TokenHash == hash && x.UserId == userId, cancellationToken)
            ?? throw new InvalidRefreshTokenException();
        if (stored.IsRevoked || stored.IsExpired(now)) throw new InvalidRefreshTokenException();
        var normalizedSlug = tenantSlug.Trim().ToLowerInvariant();
        var tenantId = await dbContext.TenantUsers
            .Where(x => x.UserId == userId && x.IsActive && x.Tenant.IsActive && x.Tenant.Slug == normalizedSlug)
            .Select(x => (Guid?)x.TenantId).SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidCredentialsException();
        var raw = GenerateRefreshToken(); var replacementHash = Hash(raw);
        stored.Revoke(now, replacementHash);
        dbContext.RefreshTokens.Add(new RefreshToken(userId, replacementHash, stored.FamilyId, now,
            now.AddDays(options.Value.RefreshTokenDays), tenantId));
        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildSession(stored.User, raw, tenantId, cancellationToken);
    }

    public async Task<CurrentUser?> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.Include(x => x.Roles).ThenInclude(x => x.Role)
            .ThenInclude(x => x.Permissions).ThenInclude(x => x.Permission)
            .SingleOrDefaultAsync(x => x.Id == userId && x.IsActive, cancellationToken);
        return user is null ? null : new CurrentUser(user.Id, user.Email, Permissions(user));
    }

    private async Task<AuthenticatedSession> CreateSessionAsync(User user, Guid familyId, CancellationToken ct)
    {
        user = await LoadUserAsync(user.NormalizedEmail, ct) ?? user;
        var raw = GenerateRefreshToken(); var now = timeProvider.GetUtcNow();
        dbContext.RefreshTokens.Add(new RefreshToken(user.Id, Hash(raw), familyId, now, now.AddDays(options.Value.RefreshTokenDays)));
        await dbContext.SaveChangesAsync(ct); return await BuildSession(user, raw, null, ct);
    }

    private async Task<AuthenticatedSession> BuildSession(User user, string refreshToken, Guid? tenantId, CancellationToken ct)
    {
        var roles = user.Roles.Select(x => x.Role.Name).Distinct().ToArray();
        // Global identity permissions always travel in the token. When a tenant is selected we also
        // embed the tenant-scoped permissions from the same source of truth the tenant context
        // middleware uses, so anything reading the JWT (e.g. the SPA sidebar) sees the real grants.
        var permissions = Permissions(user).ToList();
        if (tenantId.HasValue)
            permissions.AddRange(await tenancy.GetPermissionsAsync(tenantId.Value, user.Id, ct));
        var access = tokenGenerator.Generate(user.Id, user.Email, roles, permissions.Distinct().ToArray(), tenantId);
        return new AuthenticatedSession(access.Token, access.ExpiresAt, refreshToken);
    }

    private Task<User?> LoadUserAsync(string normalizedEmail, CancellationToken ct) => dbContext.Users
        .Include(x => x.Roles).ThenInclude(x => x.Role).ThenInclude(x => x.Permissions).ThenInclude(x => x.Permission)
        .SingleOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, ct);
    private static string[] Permissions(User user) => user.Roles.SelectMany(x => x.Role.Permissions).Select(x => x.Permission.Name).Distinct().ToArray();
    private static string NormalizeEmail(string email) => email.Trim().ToUpperInvariant();
    private static string GenerateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
