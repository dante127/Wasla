using Wasla.BuildingBlocks.Application;
using Wasla.Identity.Application.Abstractions;

namespace Wasla.Identity.Application.Roles;

public sealed record RoleListItem(Guid Id, string Name, bool IsSystem, IReadOnlyCollection<string> Permissions);

/// <summary>Lists the roles of the current tenant.</summary>
public sealed class ListRolesHandler(
    ITenantContext tenantContext,
    IRoleRepository roles)
{
    public async Task<Result<IReadOnlyList<RoleListItem>>> HandleAsync(CancellationToken cancellationToken)
    {
        if (tenantContext.TenantId is not { } tenantId)
        {
            return Result.Failure<IReadOnlyList<RoleListItem>>(
                new Error("tenancy.no_context", "No tenant context for the current request."));
        }

        var roleList = await roles.ListByTenantAsync(tenantId, cancellationToken);

        var items = roleList
            .OrderBy(role => role.Name, StringComparer.Ordinal)
            .Select(role => new RoleListItem(role.Id.Value, role.Name, role.IsSystem, role.Permissions))
            .ToList();

        return Result.Success<IReadOnlyList<RoleListItem>>(items);
    }
}
