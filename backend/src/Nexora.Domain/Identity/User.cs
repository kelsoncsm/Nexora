namespace Nexora.Domain.Identity;

public sealed class User
{
    private User() { }

    public User(string email, string normalizedEmail, DateTimeOffset createdAt)
    {
        Id = Guid.NewGuid();
        Email = email;
        NormalizedEmail = normalizedEmail;
        CreatedAt = createdAt;
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public ICollection<UserRole> Roles { get; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; } = [];

    public void SetPasswordHash(string passwordHash) => PasswordHash = passwordHash;
    public void Activate() => IsActive = true;
    public void Deactivate() => IsActive = false;
}
