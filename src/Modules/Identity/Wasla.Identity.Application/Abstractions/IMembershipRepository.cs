using Wasla.BuildingBlocks.Domain;
using Wasla.Identity.Domain;

namespace Wasla.Identity.Application.Abstractions;

public interface IMembershipRepository
{
    Task AddAsync(Membership membership, CancellationToken cancellationToken);

    Task<Membership?> GetAsync(TenantId tenantId, UserId userId, CancellationToken cancellationToken);

    Task<Membership?> GetByIdAsync(MembershipId membershipId, CancellationToken cancellationToken);

    Task<List<Membership>> ListByUserAsync(UserId userId, CancellationToken cancellationToken);

    Task<List<Membership>> ListByTenantAsync(TenantId tenantId, CancellationToken cancellationToken);

    Task<int> CountActiveWithRoleAsync(
        TenantId tenantId,
        RoleId roleId,
        MembershipId? excluding,
        CancellationToken cancellationToken);
}
