using Wasla.BuildingBlocks.Application;
using Wasla.Identity.Application.Abstractions;

namespace Wasla.Identity.Application.Users;

/// <summary>Lists users who have a membership in the current tenant.</summary>
public sealed class ListUsersHandler(
    ITenantContext tenantContext,
    IUserRepository users,
    IMembershipRepository memberships)
{
    public async Task<Result<IReadOnlyList<UserListItem>>> HandleAsync(CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<IReadOnlyList<UserListItem>>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        var tenantMemberships = await memberships.ListByTenantAsync(tenantId, cancellationToken);

        var userIds = tenantMemberships.Select(membership => membership.UserId).Distinct().ToList();
        var userList = await users.GetByIdsAsync(userIds, cancellationToken);
        var usersById = userList.ToDictionary(user => user.Id.Value);

        var items = tenantMemberships
            .Where(membership => usersById.ContainsKey(membership.UserId.Value))
            .Select(membership =>
            {
                var user = usersById[membership.UserId.Value];

                return new UserListItem(
                    user.Id.Value,
                    user.DisplayName,
                    user.Email.Value,
                    user.Status.ToString(),
                    membership.Roles.Select(role => role.RoleId.Value).ToList(),
                    membership.JoinedAt);
            })
            .ToList();

        return Result.Success<IReadOnlyList<UserListItem>>(items);
    }
}
