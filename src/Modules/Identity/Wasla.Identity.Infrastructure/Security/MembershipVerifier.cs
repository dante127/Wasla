using Wasla.BuildingBlocks.Domain;
using Wasla.Identity.Application.Abstractions;
using Wasla.Identity.Application.Contracts;
using Wasla.Identity.Domain;

namespace Wasla.Identity.Infrastructure.Security;

public sealed class MembershipVerifier(IMembershipRepository memberships) : IMembershipVerifier
{
    public async Task<bool> IsActiveMemberAsync(
        TenantId tenantId,
        UserId userId,
        CancellationToken cancellationToken)
    {
        var membership = await memberships.GetAsync(tenantId, userId, cancellationToken);

        return membership is not null && membership.Status == MembershipStatus.Active;
    }
}
