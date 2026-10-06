using Microsoft.Extensions.Options;
using Wasla.Audit.Application;
using Wasla.BuildingBlocks.Application;
using Wasla.Identity.Application.Abstractions;
using Wasla.Identity.Application.Contracts;
using Wasla.Identity.Domain;

namespace Wasla.Identity.Application.Auth;

/// <summary>
/// Rotates a refresh token: validates, revokes the old token, issues a new pair.
/// Reuse of an already-rotated token revokes the whole chain (theft detection).
/// </summary>
public sealed class RefreshHandler(
    IUserRepository users,
    IMembershipRepository memberships,
    IPermissionResolver permissions,
    ITokenService tokenService,
    IRefreshTokenRepository refreshTokens,
    IAuditWriter audit,
    IIdentityUnitOfWork unitOfWork,
    IClock clock,
    IOptions<JwtOptions> jwtOptions)
{
    public async Task<Result<RefreshResponse>> HandleAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var invalid = new Error("auth.refresh_token_invalid", "Refresh token is invalid.");

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return Result.Failure<RefreshResponse>(invalid);
        }

        var token = await refreshTokens.GetByHashAsync(
            tokenService.HashRefreshToken(request.RefreshToken),
            cancellationToken);

        if (token is null)
        {
            return Result.Failure<RefreshResponse>(invalid);
        }

        if (token.RevokedAt is not null)
        {
            // Reuse of a rotated/revoked token: revoke every active token for this user+tenant.
            await refreshTokens.RevokeAllActiveForUserAsync(token.TenantId, token.UserId, now, cancellationToken);
            await audit.RecordAsync(
                new AuditRecord(token.TenantId, token.UserId, "auth.refresh_token_reuse", "RefreshToken", token.Id.ToString(), null),
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Failure<RefreshResponse>(invalid);
        }

        if (!token.IsActive(now))
        {
            return Result.Failure<RefreshResponse>(
                new Error("auth.refresh_token_expired", "Refresh token has expired."));
        }

        var user = await users.GetByIdAsync(token.UserId, cancellationToken);

        if (user is null || user.Status != UserStatus.Active)
        {
            return Result.Failure<RefreshResponse>(invalid);
        }

        var membership = await memberships.GetAsync(token.TenantId, token.UserId, cancellationToken);

        if (membership is null || membership.Status != MembershipStatus.Active)
        {
            return Result.Failure<RefreshResponse>(
                new Error("auth.no_active_membership", "No active tenant membership exists for this user."));
        }

        var roleIds = membership.Roles.Select(role => role.RoleId).ToList();
        var effectivePermissions = await permissions.GetPermissionsAsync(membership.TenantId, roleIds, cancellationToken);

        token.Revoke(now);

        var newRefreshTokenValue = tokenService.GenerateRefreshToken();
        var newRefreshToken = RefreshToken.Issue(
            membership.TenantId,
            user.Id,
            tokenService.HashRefreshToken(newRefreshTokenValue),
            now.AddDays(jwtOptions.Value.RefreshTokenDays),
            now,
            token.Id);

        await refreshTokens.AddAsync(newRefreshToken, cancellationToken);

        var accessToken = tokenService.CreateAccessToken(user, membership.TenantId, membership.Id, effectivePermissions);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new RefreshResponse(
            accessToken.Token,
            accessToken.ExpiresAt,
            newRefreshTokenValue));
    }
}
