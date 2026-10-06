using Wasla.BuildingBlocks.Domain;
using Wasla.Identity.Application.Abstractions;
using Wasla.Identity.Domain;

namespace Wasla.Identity.Application.Permissions;

/// <summary>Resolves permissions as the union of the permissions of the given roles.</summary>
public sealed class RolePermissionResolver(IRoleRepository roles) : IPermissionResolver
{
    public async Task<IReadOnlyCollection<string>> GetPermissionsAsync(
        TenantId tenantId,
        IReadOnlyCollection<RoleId> roleIds,
        CancellationToken cancellationToken)
    {
        if (roleIds.Count == 0)
        {
            return [];
        }

        var roleList = await roles.GetByIdsAsync(tenantId, roleIds, cancellationToken);

        return roleList
            .SelectMany(role => role.Permissions)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(permission => permission, StringComparer.Ordinal)
            .ToArray();
    }
}
