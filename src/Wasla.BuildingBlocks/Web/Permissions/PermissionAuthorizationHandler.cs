using Microsoft.AspNetCore.Authorization;
using Wasla.BuildingBlocks.Application.Security;

namespace Wasla.BuildingBlocks.Web.Permissions;

/// <summary>Grants access when the authenticated principal carries the required permission claim.</summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var hasPermission = context.User.Claims.Any(claim =>
            claim.Type == WaslaClaimTypes.Permission
            && string.Equals(claim.Value, requirement.Permission, StringComparison.Ordinal));

        if (hasPermission)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
