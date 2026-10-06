using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Domain;

namespace Wasla.BuildingBlocks.Infrastructure;

/// <summary>
/// Tenant context with no tenant resolvable. Used for design-time tooling and privileged
/// system operations where tenant query filters must be disabled intentionally.
/// </summary>
public sealed class UnresolvedTenantContext : ITenantContext
{
    public static readonly UnresolvedTenantContext Instance = new();

    private UnresolvedTenantContext()
    {
    }

    public bool IsResolved => false;

    public TenantId? TenantId => null;
}
