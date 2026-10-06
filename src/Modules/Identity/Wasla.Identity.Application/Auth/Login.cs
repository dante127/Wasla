using Microsoft.Extensions.Options;
using Wasla.Audit.Application;
using Wasla.BuildingBlocks.Application;
using Wasla.Identity.Application.Abstractions;
using Wasla.Identity.Application.Contracts;
using Wasla.Identity.Domain;
using Wasla.Tenancy.Application.Contracts;

namespace Wasla.Identity.Application.Auth;

/// <summary>
/// Authenticates a user and issues a tenant-scoped token pair. Multi-tenant users must
/// select a membership explicitly.
/// </summary>
public sealed class LoginHandler(
    IUserRepository users,
    IMembershipRepository memberships,
    IPermissionResolver permissions,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    IRefreshTokenRepository refreshTokens,
    ITenantInfoProvider tenants,
    IAuditWriter audit,
    IIdentityUnitOfWork unitOfWork,
    IClock clock,
    IOptions<JwtOptions> jwtOptions)
{
    public async Task<LoginOutcome> HandleAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var invalidCredentials = new Error("auth.invalid_credentials", "Invalid email or password.");

        EmailAddress email;

        try
        {
            email = EmailAddress.Create(request.Email);
        }
        catch (ArgumentException)
        {
            return new LoginOutcome.Failure(invalidCredentials);
        }

        var user = await users.GetByEmailAsync(email, cancellationToken);

        if (user is null || user.PasswordHash is null || !passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            await audit.RecordAsync(
                new AuditRecord(null, user?.Id, "auth.login_failed", "User", user?.Id.ToString(), $"email={email.Value}"),
                cancellationToken);

            return new LoginOutcome.Failure(invalidCredentials);
        }

        if (user.Status != UserStatus.Active)
        {
            return new LoginOutcome.Failure(new Error("auth.account_disabled", "This account is not active."));
        }

        var activeMemberships = (await memberships.ListByUserAsync(user.Id, cancellationToken))
            .Where(membership => membership.Status == MembershipStatus.Active)
            .ToList();

        if (activeMemberships.Count == 0)
        {
            return new LoginOutcome.Failure(
                new Error("auth.no_active_membership", "No active tenant membership exists for this user."));
        }

        Membership membership;

        if (request.MembershipId is { } requestedMembershipId)
        {
            var match = activeMemberships.FirstOrDefault(item => item.Id.Value == requestedMembershipId);

            if (match is null)
            {
                return new LoginOutcome.Failure(new Error("auth.membership_not_found", "Membership not found."));
            }

            membership = match;
        }
        else if (activeMemberships.Count == 1)
        {
            membership = activeMemberships[0];
        }
        else
        {
            var summaries = new List<MembershipSummary>();

            foreach (var candidate in activeMemberships)
            {
                var summary = await tenants.GetSummaryAsync(candidate.TenantId, cancellationToken);
                summaries.Add(new MembershipSummary(
                    candidate.Id.Value,
                    candidate.TenantId.Value,
                    summary?.Name ?? "Unknown",
                    summary?.Slug ?? "unknown"));
            }

            return new LoginOutcome.SelectionRequired(summaries);
        }

        var tenantInfo = await tenants.GetSummaryAsync(membership.TenantId, cancellationToken);

        if (tenantInfo is null)
        {
            return new LoginOutcome.Failure(new Error("tenancy.not_found", "Tenant not found."));
        }

        var roleIds = membership.Roles.Select(role => role.RoleId).ToList();
        var effectivePermissions = await permissions.GetPermissionsAsync(membership.TenantId, roleIds, cancellationToken);

        user.RecordLogin(now);

        var accessToken = tokenService.CreateAccessToken(user, membership.TenantId, membership.Id, effectivePermissions);
        var refreshTokenValue = tokenService.GenerateRefreshToken();

        var refreshToken = RefreshToken.Issue(
            membership.TenantId,
            user.Id,
            tokenService.HashRefreshToken(refreshTokenValue),
            now.AddDays(jwtOptions.Value.RefreshTokenDays),
            now);

        await refreshTokens.AddAsync(refreshToken, cancellationToken);
        await audit.RecordAsync(
            new AuditRecord(membership.TenantId, user.Id, "auth.login_succeeded", "User", user.Id.ToString(), null),
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new LoginOutcome.Success(new LoginResponse(
            accessToken.Token,
            accessToken.ExpiresAt,
            refreshTokenValue,
            new UserSummary(user.Id.Value, user.DisplayName, user.Email.Value),
            tenantInfo,
            effectivePermissions));
    }
}
