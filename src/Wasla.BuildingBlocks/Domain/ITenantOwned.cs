namespace Wasla.BuildingBlocks.Domain;

/// <summary>Marks an entity as owned by a tenant (row-level multi-tenancy).</summary>
public interface ITenantOwned
{
    TenantId TenantId { get; }
}
