using Wasla.BuildingBlocks.Domain;
using Wasla.Identity.Domain;

namespace Wasla.Identity.Application.Abstractions;

public interface IRoleRepository
{
    Task AddRangeAsync(IEnumerable<Role> roles, CancellationToken cancellationToken);

    Task<Role?> GetByNameAsync(TenantId tenantId, string name, CancellationToken cancellationToken);

    Task<List<Role>> GetByIdsAsync(
        TenantId tenantId,
        IReadOnlyCollection<RoleId> roleIds,
        CancellationToken cancellationToken);

    Task<List<Role>> ListByTenantAsync(TenantId tenantId, CancellationToken cancellationToken);

    Task<bool> AnyForTenantAsync(TenantId tenantId, CancellationToken cancellationToken);
}
