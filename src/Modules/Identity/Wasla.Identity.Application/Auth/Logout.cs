using Wasla.BuildingBlocks.Application;
using Wasla.Identity.Application.Abstractions;

namespace Wasla.Identity.Application.Auth;

/// <summary>Revokes the presented refresh token (idempotent).</summary>
public sealed class LogoutHandler(
    ITokenService tokenService,
    IRefreshTokenRepository refreshTokens,
    IIdentityUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task HandleAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return;
        }

        var token = await refreshTokens.GetByHashAsync(
            tokenService.HashRefreshToken(request.RefreshToken),
            cancellationToken);

        if (token is null || !token.IsActive(clock.UtcNow))
        {
            return;
        }

        token.Revoke(clock.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
