using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Domain;
using Wasla.Identity.Application.Abstractions;
using Wasla.Tenancy.Application.Contracts;

namespace Wasla.Identity.Application.Auth;

/// <summary>Current user profile within the current tenant context, with effective permissions.</summary>
public sealed class MeHandler(
    ICurrentUser currentUser,
    ITenantContext tenantContext,
    IUserRepository users,
    IMembershipRepository memberships,
    IPermissionResolver permissions,
    ITenantInfoProvider tenants)
{
    public async Task<Result<MeResponse>> HandleAsync(CancellationToken cancellationToken)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId is not { } userId)
        {
            return Result.Failure<MeResponse>(new Error("auth.unauthorized", "Not authenticated."));
        }

        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<MeResponse>(new Error("tenancy.no_context", "No tenant context."));
        }

        var user = await users.GetByIdAsync(new UserId(userId), cancellationToken);

        if (user is null)
        {
            return Result.Failure<MeResponse>(new Error("users.not_found", "User not found."));
        }

        var membership = await memberships.GetAsync(tenantId, user.Id, cancellationToken);

        if (membership is null)
        {
            return Result.Failure<MeResponse>(new Error("membership.not_found", "Membership not found."));
        }

        var tenant = await tenants.GetSummaryAsync(tenantId, cancellationToken);

        if (tenant is null)
        {
            return Result.Failure<MeResponse>(new Error("tenancy.not_found", "Tenant not found."));
        }

        var roleIds = membership.Roles.Select(role => role.RoleId).ToList();
        var effectivePermissions = await permissions.GetPermissionsAsync(tenantId, roleIds, cancellationToken);

        return Result.Success(new MeResponse(
            new UserSummary(user.Id.Value, user.DisplayName, user.Email.Value),
            tenant,
            effectivePermissions,
            membership.Id.Value));
    }
}
