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

/// <summary>Shared e-mail/password validation so every entry point applies the same policy.</summary>
public static class IdentityValidation
{
    public const int PasswordMinLength = 12;
    public const int PasswordMaxLength = 128;
    public const int EmailMaxLength = 320;

    public static void ValidateCredentials(string email, string password)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal) || email.Length > EmailMaxLength)
            errors["email"] = ["A valid email is required."];
        AddPasswordErrors(password, errors);
        if (errors.Count > 0) throw new IdentityValidationException(errors);
    }

    public static void ValidatePassword(string password)
    {
        var errors = new Dictionary<string, string[]>();
        AddPasswordErrors(password, errors);
        if (errors.Count > 0) throw new IdentityValidationException(errors);
    }

    private static void AddPasswordErrors(string? password, Dictionary<string, string[]> errors)
    {
        if (password is null || password.Length < PasswordMinLength || password.Length > PasswordMaxLength)
            errors["password"] = [$"Password must contain between {PasswordMinLength} and {PasswordMaxLength} characters."];
    }
}

public interface IIdentityService
{
    Task<AuthenticatedSession> RegisterAsync(RegisterCommand command, CancellationToken cancellationToken);
    Task<AuthenticatedSession> LoginAsync(LoginCommand command, CancellationToken cancellationToken);
    Task<AuthenticatedSession> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken);
    Task<AuthenticatedSession> SelectTenantAsync(Guid userId, string tenantSlug, string refreshToken, CancellationToken cancellationToken);
    Task<CurrentUser?> GetUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new identity for an invited e-mail, reusing the exact registration path (same
    /// password policy, hasher, e-mail normalisation, default role and welcome e-mail). Entities are
    /// added to the current unit of work but <b>not</b> saved — the caller commits the user together
    /// with the tenant membership. Assumes the e-mail is not already registered.
    /// </summary>
    Task<Guid> ProvisionInvitedUserAsync(string email, string password, Guid tenantId, CancellationToken cancellationToken);

    /// <summary>Verifies a password against a stored hash for the given e-mail. False if the user does not exist.</summary>
    Task<Guid?> VerifyCredentialsAsync(string normalizedEmail, string password, CancellationToken cancellationToken);
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
