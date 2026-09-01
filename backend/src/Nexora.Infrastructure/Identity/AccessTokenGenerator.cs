using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Nexora.Application.Identity;

namespace Nexora.Infrastructure.Identity;

public sealed class AccessTokenGenerator(IOptions<IdentityOptions> options, TimeProvider timeProvider)
    : IAccessTokenGenerator
{
    public AccessTokenResult Generate(Guid userId, string email, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> permissions, Guid? tenantId = null)
    {
        var settings = options.Value;
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(settings.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(x => new Claim(ClaimTypes.Role, x)));
        claims.AddRange(permissions.Select(x => new Claim("permission", x)));
        if (tenantId.HasValue) claims.Add(new Claim("tenant_id", tenantId.Value.ToString()));
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims, now.UtcDateTime,
            expiresAt.UtcDateTime, credentials);
        return new AccessTokenResult(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }
}
