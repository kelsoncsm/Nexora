namespace Nexora.Application.Identity;

public sealed record RegisterCommand(string Email, string Password);
public sealed record LoginCommand(string Email, string Password);
public sealed record AuthenticatedSession(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken);
public sealed record CurrentUser(Guid Id, string Email, IReadOnlyCollection<string> Permissions);

/// <summary>
/// Permission keys that belong to the user's global identity and must survive the
/// per-request tenant permission swap done by the tenant context middleware.
/// </summary>
public static class GlobalPermissions
{
    public const string Profile = "identity.profile";
    public static readonly string[] All = [Profile];
}

public interface IIdentityService
{
    Task<AuthenticatedSession> RegisterAsync(RegisterCommand command, CancellationToken cancellationToken);
    Task<AuthenticatedSession> LoginAsync(LoginCommand command, CancellationToken cancellationToken);
    Task<AuthenticatedSession> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken);
    Task<AuthenticatedSession> SelectTenantAsync(Guid userId, string tenantSlug, string refreshToken, CancellationToken cancellationToken);
    Task<CurrentUser?> GetUserAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed record AccessTokenResult(string Token, DateTimeOffset ExpiresAt);
public interface IAccessTokenGenerator
{
    AccessTokenResult Generate(Guid userId, string email, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> permissions, Guid? tenantId = null);
}

public sealed class IdentityValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("Identity request validation failed.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

public sealed class IdentityConflictException(string message) : Exception(message) { }
public sealed class InvalidCredentialsException : Exception
{
    public InvalidCredentialsException() : base("Invalid credentials.") { }
}
public sealed class InvalidRefreshTokenException : Exception
{
    public InvalidRefreshTokenException() : base("Invalid refresh token.") { }
}
