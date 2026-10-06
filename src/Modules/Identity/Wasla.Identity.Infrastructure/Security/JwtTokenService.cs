using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Application.Security;
using Wasla.BuildingBlocks.Domain;
using Wasla.Identity.Application.Abstractions;
using Wasla.Identity.Application.Contracts;
using Wasla.Identity.Domain;

namespace Wasla.Identity.Infrastructure.Security;

/// <summary>
/// Issues HS256 access tokens and secure random refresh tokens. Refresh tokens are
/// returned to clients once; only their SHA-256 hashes are persisted.
/// </summary>
public sealed class JwtTokenService(IOptions<JwtOptions> options, IClock clock) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public AccessToken CreateAccessToken(
        User user,
        TenantId tenantId,
        MembershipId membershipId,
        IReadOnlyCollection<string> permissions)
    {
        var now = clock.UtcNow;
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(WaslaClaimTypes.Subject, user.Id.Value.ToString()),
            new(WaslaClaimTypes.Email, user.Email.Value),
            new(WaslaClaimTypes.Name, user.DisplayName),
            new(WaslaClaimTypes.TenantId, tenantId.Value.ToString()),
            new(WaslaClaimTypes.MembershipId, membershipId.Value.ToString()),
        };

        claims.AddRange(permissions.Select(permission => new Claim(WaslaClaimTypes.Permission, permission)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new AccessToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        return Convert.ToHexString(bytes);
    }

    public string HashRefreshToken(string refreshToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
