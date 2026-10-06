using Microsoft.EntityFrameworkCore;
using Wasla.BuildingBlocks.Domain;
using Wasla.Identity.Application.Abstractions;
using Wasla.Identity.Domain;

namespace Wasla.Identity.Infrastructure.Repositories;

public sealed class RoleRepository(IdentityDbContext dbContext) : IRoleRepository
{
    public async Task AddRangeAsync(IEnumerable<Role> roles, CancellationToken cancellationToken) =>
        await dbContext.Roles.AddRangeAsync(roles, cancellationToken);

    public Task<Role?> GetByNameAsync(TenantId tenantId, string name, CancellationToken cancellationToken) =>
        dbContext.Roles.FirstOrDefaultAsync(
            role => role.TenantId == tenantId && role.Name == name,
            cancellationToken);

    public async Task<List<Role>> GetByIdsAsync(
        TenantId tenantId,
        IReadOnlyCollection<RoleId> roleIds,
        CancellationToken cancellationToken)
    {
        var ids = roleIds.Distinct().ToList();

        return await dbContext.Roles
            .Where(role => role.TenantId == tenantId && ids.Contains(role.Id))
            .ToListAsync(cancellationToken);
    }

    public Task<List<Role>> ListByTenantAsync(TenantId tenantId, CancellationToken cancellationToken) =>
        dbContext.Roles
            .Where(role => role.TenantId == tenantId)
            .OrderBy(role => role.Name)
            .ToListAsync(cancellationToken);

    public Task<bool> AnyForTenantAsync(TenantId tenantId, CancellationToken cancellationToken) =>
        dbContext.Roles.AnyAsync(role => role.TenantId == tenantId, cancellationToken);
}
