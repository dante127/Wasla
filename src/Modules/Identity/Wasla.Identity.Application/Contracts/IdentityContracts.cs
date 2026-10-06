using Wasla.BuildingBlocks.Domain;

namespace Wasla.Identity.Application.Contracts;

/// <summary>Module contract: verify user membership without touching Identity internals.</summary>
public interface IMembershipVerifier
{
    Task<bool> IsActiveMemberAsync(TenantId tenantId, UserId userId, CancellationToken cancellationToken);
}
