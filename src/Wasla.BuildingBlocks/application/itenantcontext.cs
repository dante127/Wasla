using Wasla.BuildingBlocks.Domain;

namespace Wasla.BuildingBlocks.Application;

/// <summary>Ambient tenant context for the current request or background-job scope.</summary>
public interface ITenantContext
{
    bool IsResolved { get; }

    TenantId? TenantId { get; }
}
