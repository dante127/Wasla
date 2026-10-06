using Wasla.BuildingBlocks.Application;
using Wasla.BuildingBlocks.Domain;

namespace Wasla.BuildingBlocks.Infrastructure;

/// <summary>Scoped tenant context; populated by tenant-resolution middleware from the access token.</summary>
public sealed class TenantContext : ITenantContext
{
    public bool IsResolved => TenantId is not null;

    public TenantId? TenantId { get; private set; }

    public void Resolve(TenantId tenantId) => TenantId = tenantId;
}
