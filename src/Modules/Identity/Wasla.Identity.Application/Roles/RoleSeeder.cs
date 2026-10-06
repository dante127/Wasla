using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Domain;
using Wasla.Identity.Application.Abstractions;
using Wasla.Identity.Domain;

namespace Wasla.Identity.Application.Roles;

/// <summary>Creates the seeded system roles for a tenant (idempotent).</summary>
public sealed class DefaultRoleSeeder(
    IRoleRepository roles,
    IIdentityUnitOfWork unitOfWork)
{
    public async Task SeedAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        if (await roles.AnyForTenantAsync(tenantId, cancellationToken))
        {
            return;
        }

        var roleList = SystemRoles.Definitions
            .Select(definition => Role.Create(tenantId, definition.Key, definition.Value, isSystem: true))
            .ToList();

        await roles.AddRangeAsync(roleList, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
