using Wasla.BuildingBlocks.Domain;
using Wasla.Identity.Domain;

namespace Wasla.Identity.Application.Abstractions;

public interface IRefreshTokenRepository
{
    Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken);

    Task<RefreshToken?> GetByHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>Revokes all active refresh tokens for a user in a tenant (reuse detection / logout-all).</summary>
    Task RevokeAllActiveForUserAsync(
        TenantId tenantId,
        UserId userId,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
