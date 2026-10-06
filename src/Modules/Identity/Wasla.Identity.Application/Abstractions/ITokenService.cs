using Wasla.BuildingBlocks.Domain;
using Wasla.Identity.Domain;

namespace Wasla.Identity.Application.Abstractions;

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

public interface ITokenService
{
    AccessToken CreateAccessToken(
        User user,
        TenantId tenantId,
        MembershipId membershipId,
        IReadOnlyCollection<string> permissions);

    /// <summary>Generates a new cryptographically random refresh token (returned to the client once).</summary>
    string GenerateRefreshToken();

    /// <summary>Hashes a refresh token for storage/lookup (only hashes are persisted).</summary>
    string HashRefreshToken(string refreshToken);
}
