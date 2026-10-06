using Microsoft.EntityFrameworkCore;
using Wasla.BuildingBlocks.Domain;
using Wasla.Identity.Application.Abstractions;
using Wasla.Identity.Domain;

namespace Wasla.Identity.Infrastructure.Repositories;

public sealed class MembershipRepository(IdentityDbContext dbContext) : IMembershipRepository
{
    public async Task AddAsync(Membership membership, CancellationToken cancellationToken) =>
        await dbContext.Memberships.AddAsync(membership, cancellationToken);

    public Task<Membership?> GetAsync(TenantId tenantId, UserId userId, CancellationToken cancellationToken) =>
        dbContext.Memberships
            .Include(membership => membership.Roles)
            .FirstOrDefaultAsync(
                membership => membership.TenantId == tenantId && membership.UserId == userId,
                cancellationToken);

    public Task<Membership?> GetByIdAsync(MembershipId membershipId, CancellationToken cancellationToken) =>
        dbContext.Memberships
            .Include(membership => membership.Roles)
            .FirstOrDefaultAsync(membership => membership.Id == membershipId, cancellationToken);

    public Task<List<Membership>> ListByUserAsync(UserId userId, CancellationToken cancellationToken) =>
        dbContext.Memberships
            .Include(membership => membership.Roles)
            .Where(membership => membership.UserId == userId)
            .ToListAsync(cancellationToken);

    public Task<List<Membership>> ListByTenantAsync(TenantId tenantId, CancellationToken cancellationToken) =>
        dbContext.Memberships
            .Include(membership => membership.Roles)
            .Where(membership => membership.TenantId == tenantId)
            .ToListAsync(cancellationToken);

    public Task<int> CountActiveWithRoleAsync(
        TenantId tenantId,
        RoleId roleId,
        MembershipId? excluding,
        CancellationToken cancellationToken) =>
        dbContext.Memberships
            .Where(membership => membership.TenantId == tenantId)
            .Where(membership => membership.Status == MembershipStatus.Active)
            .Where(membership => !excluding.HasValue || membership.Id != excluding.GetValueOrDefault())
            .Where(membership => membership.Roles.Any(role => role.RoleId == roleId))
            .CountAsync(cancellationToken);
}
