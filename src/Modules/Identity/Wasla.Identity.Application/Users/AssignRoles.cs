using Wasla.Audit.Application;
using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Domain;
using Wasla.Identity.Application.Abstractions;
using Wasla.Identity.Domain;

namespace Wasla.Identity.Application.Users;

/// <summary>
/// Replaces a membership's roles. Enforces the "last owner" invariant: a tenant must
/// always keep at least one active member holding the Owner role.
/// </summary>
public sealed class AssignRolesHandler(
    ITenantContext tenantContext,
    IMembershipRepository memberships,
    IRoleRepository roles,
    IAuditWriter audit,
    IIdentityUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result> HandleAsync(Guid userId, AssignRolesRequest request, CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure(new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        var membership = await memberships.GetAsync(tenantId, new UserId(userId), cancellationToken);

        if (membership is null)
        {
            return Result.Failure(new Error("users.not_found", "User not found."));
        }

        var roleIds = request.RoleIds.Distinct().Select(id => new RoleId(id)).ToList();

        if (roleIds.Count > 0)
        {
            var found = await roles.GetByIdsAsync(tenantId, roleIds, cancellationToken);

            if (found.Count != roleIds.Count)
            {
                return Result.Failure(new Error("roles.not_found", "One or more roles were not found."));
            }
        }

        var ownerRole = await roles.GetByNameAsync(tenantId, SystemRoles.Owner, cancellationToken);

        if (ownerRole is not null)
        {
            var currentlyOwner = membership.Roles.Any(role => role.RoleId == ownerRole.Id);
            var willBeOwner = roleIds.Contains(ownerRole.Id);

            if (currentlyOwner && !willBeOwner)
            {
                var otherOwners = await memberships.CountActiveWithRoleAsync(
                    tenantId,
                    ownerRole.Id,
                    membership.Id,
                    cancellationToken);

                if (otherOwners == 0)
                {
                    return Result.Failure(new Error(
                        "membership.last_owner_conflict",
                        "The tenant must always keep at least one active member with the Owner role."));
                }
            }
        }

        membership.ReplaceRoles(roleIds, clock.UtcNow);

        await audit.RecordAsync(
            new AuditRecord(tenantId, null, "users.roles_assigned", "Membership", membership.Id.ToString(), null),
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
